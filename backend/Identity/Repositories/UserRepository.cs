using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Identity.Repositories;

public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    private IQueryable<User> ReadUsers => dbContext.Users.AsNoTracking()
        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role);

    public Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        var target = normalizedUsername.Trim().ToLowerInvariant();
        return ReadUsers.FirstOrDefaultAsync(u => u.Username.Trim().ToLower() == target, cancellationToken);
    }

    public Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ReadUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task LockAdministrationAsync(CancellationToken cancellationToken = default) =>
        _ = await dbContext.Roles.FromSqlInterpolated($"SELECT * FROM roles WHERE name = {nameof(RoleType.Admin)} FOR UPDATE")
            .SingleAsync(cancellationToken);

    public Task<Role> GetRoleForAssignmentAsync(RoleType role, CancellationToken cancellationToken = default) =>
        dbContext.Roles.SingleAsync(x => x.Name == role, cancellationToken);

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().CountAsync(u => u.IsActive && u.UserRoles.Count == 1
            && u.UserRoles.Any(ur => ur.Role.Name == RoleType.Admin), cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(UserSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = ReadUsers;
        if (criteria.Keyword is { } keyword)
        {
            var text = keyword.ToLowerInvariant();
            query = query.Where(u => u.Username.ToLower().Contains(text) || u.FullName.ToLower().Contains(text) || u.Email.ToLower().Contains(text));
        }
        if (criteria.Role is { } role) query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
        if (criteria.IsActive is { } active) query = query.Where(u => u.IsActive == active);
        var count = await query.CountAsync(cancellationToken);
        var skip = (long)(criteria.Page - 1) * criteria.PageSize;
        if (skip >= count) return ([], count);
        var items = await query.OrderBy(u => u.Username).ThenBy(u => u.Id)
            .Skip((int)skip).Take(criteria.PageSize).ToListAsync(cancellationToken);
        return (items, count);
    }

    public void ReplaceRole(User user, Role role, DateTime assignedAt)
    {
        foreach (var assignment in user.UserRoles.Where(x => x.RoleId != role.Id).ToArray())
        {
            dbContext.UserRoles.Remove(assignment);
            user.UserRoles.Remove(assignment);
        }
        if (!user.UserRoles.Any(x => x.RoleId == role.Id))
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role, AssignedAt = assignedAt });
    }

    public void Add(User user) => dbContext.Users.Add(user);
}
