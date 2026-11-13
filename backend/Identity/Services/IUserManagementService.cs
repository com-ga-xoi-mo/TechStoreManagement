using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public sealed record UserOperationResult<T>(int StatusCode, T? Data = default,
    Dictionary<string, string[]>? Errors = null, string? Detail = null);

public interface IUserManagementService
{
    Task<UserOperationResult<PagedResultDto<UserAccountDto>>> SearchAsync(UserSearchRequest request, CancellationToken cancellationToken = default);
    Task<UserOperationResult<UserAccountDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserOperationResult<UserAccountDto>> CreateAsync(CreateUserRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
    Task<UserOperationResult<UserAccountDto>> UpdateAsync(Guid id, UpdateUserRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
    Task<UserOperationResult<UserAccountDto>> SetStatusAsync(Guid id, UpdateUserStatusRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
    Task<UserOperationResult<UserAccountDto>> SetRoleAsync(Guid id, UpdateUserRoleRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
    Task<UserOperationResult<UserAccountDto>> ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
}
