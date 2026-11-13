using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Identity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task LockAdministrationAsync(CancellationToken cancellationToken = default);
    Task<Role> GetRoleForAssignmentAsync(RoleType role, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(UserSearchCriteria criteria, CancellationToken cancellationToken = default);
    void ReplaceRole(User user, Role role, DateTime assignedAt);
    void Add(User user);
}
