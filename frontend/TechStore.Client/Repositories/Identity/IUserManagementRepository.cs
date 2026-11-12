using TechStore.Client.Models.Identity;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Identity;

public interface IUserManagementRepository
{
    Task<UserAccountSearchResult> SearchAsync(UserAccountQuery query, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserAccount> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<UserAccount> SetRoleAsync(Guid id, string role, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(Guid id, string password, CancellationToken cancellationToken = default);
}
