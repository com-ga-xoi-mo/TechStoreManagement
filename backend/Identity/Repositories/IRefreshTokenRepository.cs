using TechStore.Api.Identity.Entities;

namespace TechStore.Api.Identity.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetActiveTokenAsync(Guid userId, string tokenHash, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(RefreshToken refreshToken);
}
