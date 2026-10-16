using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;

namespace TechStore.UnitTests;

public class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];
    public List<RefreshToken> AddedTokens { get; } = [];

    public Task<RefreshToken?> GetActiveTokenAsync(Guid userId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var token = Tokens.FirstOrDefault(t => t.UserId == userId && t.TokenHash == tokenHash && !t.IsRevoked);
        return Task.FromResult(token);
    }

    public Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = Tokens.Where(t => t.UserId == userId && !t.IsRevoked).ToList();
        return Task.FromResult(active);
    }

    public void Add(RefreshToken refreshToken)
    {
        Tokens.Add(refreshToken);
        AddedTokens.Add(refreshToken);
    }
}
