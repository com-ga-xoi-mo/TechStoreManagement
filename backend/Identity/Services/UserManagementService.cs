using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public sealed class UserManagementService(IUserRepository users, IRefreshTokenRepository tokens,
    IUnitOfWork work, TimeProvider time) : IUserManagementService
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<UserOperationResult<PagedResultDto<UserAccountDto>>> SearchAsync(UserSearchRequest request, CancellationToken cancellationToken = default)
    {
        RoleType? role = null;
        if (request.Role is not null)
        {
            if (!TryRole(request.Role, out var parsed)) return new(400, Errors: new() { ["role"] = ["Vai trò không hợp lệ."] });
            role = parsed;
        }
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var criteria = new UserSearchCriteria(string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(), role, request.IsActive, page, size);
        var (items, count) = await users.SearchAsync(criteria, cancellationToken);
        return new(200, new(items.Select(Map).ToList(), page, size, count, (int)Math.Ceiling((double)count / size)));
    }

    public async Task<UserOperationResult<UserAccountDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await users.GetByIdWithRolesAsync(id, cancellationToken) is { } user ? new(200, Map(user)) : new(404);

    public Task<UserOperationResult<UserAccountDto>> CreateAsync(CreateUserRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default)
    {
        var errors = ProfileErrors(request.FullName, request.Email);
        if (!Regex.IsMatch(request.Username?.Trim() ?? "", "^[A-Za-z0-9._-]{3,64}$", RegexOptions.CultureInvariant))
            errors["username"] = ["Tên đăng nhập cần 3–64 chữ, số hoặc dấu . _ -."];
        PasswordErrors(request.Password, errors);
        if (!TryRole(request.Role, out var role)) errors["role"] = ["Vai trò không hợp lệ."];
        if (errors.Count != 0) return Task.FromResult(new UserOperationResult<UserAccountDto>(400, Errors: errors));
        return WriteAsync(null, actor, async (_, ct) =>
        {
            var user = new User
            {
                Id = Guid.NewGuid(), Username = request.Username!.Trim().ToLowerInvariant(),
                FullName = request.FullName.Trim(), Email = request.Email.Trim(), CreatedAt = time.GetUtcNow().UtcDateTime
            };
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            var assignedRole = await users.GetRoleForAssignmentAsync(role, ct);
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = assignedRole.Id, Role = assignedRole, AssignedAt = user.CreatedAt });
            users.Add(user);
            return new(201, Map(user));
        }, cancellationToken);
    }

    public Task<UserOperationResult<UserAccountDto>> UpdateAsync(Guid id, UpdateUserRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default)
    {
        var errors = ProfileErrors(request.FullName, request.Email);
        if (errors.Count != 0) return Task.FromResult(new UserOperationResult<UserAccountDto>(400, Errors: errors));
        return WriteAsync(id, actor, (user, _) =>
        {
            user!.FullName = request.FullName.Trim();
            user.Email = request.Email.Trim();
            user.UpdatedAt = time.GetUtcNow().UtcDateTime;
            return Task.FromResult(new UserOperationResult<UserAccountDto>(200, Map(user)));
        }, cancellationToken);
    }

    public Task<UserOperationResult<UserAccountDto>> SetStatusAsync(Guid id, UpdateUserStatusRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default) =>
        request.IsActive is null ? Task.FromResult(new UserOperationResult<UserAccountDto>(400, Errors: new() { ["isActive"] = ["Cần chọn trạng thái."] }))
            : ChangeAccessAsync(id, actor, request.IsActive, null, null, cancellationToken);

    public Task<UserOperationResult<UserAccountDto>> SetRoleAsync(Guid id, UpdateUserRoleRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default) =>
        !TryRole(request.Role, out var role) ? Task.FromResult(new UserOperationResult<UserAccountDto>(400, Errors: new() { ["role"] = ["Vai trò không hợp lệ."] }))
            : ChangeAccessAsync(id, actor, null, role, null, cancellationToken);

    public Task<UserOperationResult<UserAccountDto>> ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        PasswordErrors(request.Password, errors);
        return errors.Count != 0 ? Task.FromResult(new UserOperationResult<UserAccountDto>(400, Errors: errors))
            : ChangeAccessAsync(id, actor, null, null, request.Password, cancellationToken);
    }

    private Task<UserOperationResult<UserAccountDto>> ChangeAccessAsync(Guid id, AuthenticatedUser actor, bool? active,
        RoleType? role, string? password, CancellationToken cancellationToken) => WriteAsync(id, actor, async (target, ct) =>
    {
        var user = target!;
        var currentRole = AuthenticatedUser.SoleRole(user);
        if (password is null && (!active.HasValue || active == user.IsActive) && (!role.HasValue || role == currentRole))
            return new(200, Map(user));
        if (user.IsActive && currentRole == RoleType.Admin && (active == false || (role.HasValue && role != RoleType.Admin))
            && await users.CountActiveAdminsAsync(ct) <= 1)
            return new(409, Detail: "Không thể khóa hoặc hạ quyền Admin hoạt động cuối cùng.");
        var now = time.GetUtcNow().UtcDateTime;
        if (active.HasValue) user.IsActive = active.Value;
        if (role.HasValue) users.ReplaceRole(user, await users.GetRoleForAssignmentAsync(role.Value, ct), now);
        if (password is not null) user.PasswordHash = _hasher.HashPassword(user, password);
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = now;
        foreach (var token in await tokens.GetActiveTokensByUserIdAsync(id, ct))
        {
            token.IsRevoked = true;
            token.UpdatedAt = now;
        }
        return new(200, Map(user));
    }, cancellationToken);

    private async Task<UserOperationResult<UserAccountDto>> WriteAsync(Guid? id, AuthenticatedUser actor,
        Func<User?, CancellationToken, Task<UserOperationResult<UserAccountDto>>> change, CancellationToken cancellationToken)
    {
        await work.BeginTransactionAsync(cancellationToken);
        try
        {
            await users.LockAdministrationAsync(cancellationToken);
            var locked = new Dictionary<Guid, User?>();
            foreach (var userId in new[] { actor.Id, id ?? actor.Id }.Distinct().Order())
                locked[userId] = await users.GetForUpdateAsync(userId, cancellationToken);
            var caller = locked[actor.Id];
            UserOperationResult<UserAccountDto> result;
            if (caller is not { IsActive: true } || caller.TokenVersion != actor.TokenVersion) result = new(401);
            else if (actor.Role != RoleType.Admin || AuthenticatedUser.SoleRole(caller) != RoleType.Admin) result = new(403);
            else if (id.HasValue && locked[id.Value] is null) result = new(404);
            else result = await change(id.HasValue ? locked[id.Value] : null, cancellationToken);
            if (result.StatusCode >= 400)
            {
                await work.RollbackAsync(CancellationToken.None);
                return result;
            }
            await work.SaveChangesAsync(cancellationToken);
            await work.CommitAsync(cancellationToken);
            return result;
        }
        catch (DuplicateKeyException)
        {
            await work.RollbackAsync(CancellationToken.None);
            return new(409, Detail: "Tên đăng nhập hoặc email đã được sử dụng.");
        }
        catch
        {
            await work.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool TryRole(string? value, out RoleType role)
    {
        role = default;
        return Enum.GetNames<RoleType>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            && Enum.TryParse(value, true, out role);
    }

    private static Dictionary<string, string[]> ProfileErrors(string? fullName, string? email)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 200) errors["fullName"] = ["Họ tên cần 1–200 ký tự."];
        var trimmed = email?.Trim();
        if (trimmed is null || trimmed.Length > 254 || !MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
            errors["email"] = ["Email không hợp lệ."];
        return errors;
    }

    private static void PasswordErrors(string? password, Dictionary<string, string[]> errors)
    {
        if (password is null || password.Length is < 8 or > 128) errors["password"] = ["Mật khẩu cần 8–128 ký tự."];
    }

    private static UserAccountDto Map(User user) => new()
    {
        Id = user.Id, Username = user.Username, FullName = user.FullName, Email = user.Email,
        Role = AuthenticatedUser.SoleRole(user)?.ToString(), IsActive = user.IsActive,
        CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt
    };
}
