using TechStore.Client.Services.Session;

namespace TechStore.Client.Services.Authentication;

public interface IAccessTokenProvider
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>The identity feature supplies/clears this in-memory session after login/logout.</summary>
public sealed class SessionAccessTokenProvider(ISessionStore? sessionStore = null) : IAccessTokenProvider
{
    private string? _accessToken;

    public void SetAccessToken(string? accessToken) => Interlocked.Exchange(ref _accessToken, accessToken);

    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = sessionStore?.Current.AccessToken ?? Volatile.Read(ref _accessToken);
        return ValueTask.FromResult<string?>(token);
    }
}
