using System.Net.Http.Json;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Services.Session;

public sealed class SessionRefreshCoordinator(IHttpClientFactory clients, ISessionStore store, ApiClientOptions options)
{
    private readonly object _gate = new();
    private Task? _pending;
    private long _generation;
    private string? _refreshToken;

    public async Task EnsureFreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = store.Current;
        if (snapshot.Status != SessionStatus.Active || string.IsNullOrWhiteSpace(snapshot.RefreshToken)
            || snapshot.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60)) return;
        Task pending;
        lock (_gate)
        {
            if (_pending is null || _generation != snapshot.Generation || _refreshToken != snapshot.RefreshToken)
            {
                _generation = snapshot.Generation;
                _refreshToken = snapshot.RefreshToken;
                _pending = RefreshAsync(snapshot);
            }
            pending = _pending;
        }
        await pending.WaitAsync(cancellationToken);
        if (store.Current.Generation != snapshot.Generation || store.Current.Status != SessionStatus.Active)
            throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Vui lòng thử lại.");
    }

    private async Task RefreshAsync(SessionSnapshot snapshot)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            using var client = clients.CreateClient("SessionRefresh");
            client.BaseAddress = options.Validate();
            using var response = await client.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = snapshot.RefreshToken! }, timeout.Token);
            response.EnsureSuccessStatusCode();
            var pair = await response.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken: timeout.Token)
                ?? throw new InvalidOperationException("Phản hồi làm mới phiên không hợp lệ.");
            var current = store.Current;
            if (current.Generation != snapshot.Generation || current.AccessToken != snapshot.AccessToken
                || current.RefreshToken != snapshot.RefreshToken) return;
            store.TrySetSession(snapshot.Generation, pair, snapshot.RememberMe, renewal: true, expectedAccessToken: snapshot.AccessToken, expectedRefreshToken: snapshot.RefreshToken);
        }
        catch (Exception exception)
        {
            store.TryExpire(snapshot.Generation, snapshot.AccessToken);
            throw new InvalidOperationException("Không thể khôi phục phiên. Vui lòng đăng nhập lại.", exception);
        }
    }
}
