using TechStore.Api.Identity.Entities;

namespace TechStore.Api.Identity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(User user);
}
