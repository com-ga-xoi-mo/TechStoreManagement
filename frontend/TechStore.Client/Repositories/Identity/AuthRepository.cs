using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Identity;

public sealed class AuthRepository : IAuthRepository
{
    private readonly IApiClient _apiClient;
    private readonly ISessionStore _sessionStore;

    public AuthRepository(IApiClient apiClient, ISessionStore sessionStore)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var generation = _sessionStore.Current.Generation;
        var response = await _apiClient.PostAsync<LoginRequest, LoginResponseDto>("api/auth/login", request, cancellationToken);
        if (response != null && !string.IsNullOrEmpty(response.AccessToken))
        {
            if (!_sessionStore.TrySetSession(generation, response, request.RememberMe))
                throw new OperationCanceledException("Phiên đăng nhập đã thay đổi.");
        }
        return response!;
    }

    public async Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var generation = _sessionStore.Current.Generation;
        var profile = await _apiClient.GetAsync<UserProfileDto>("api/auth/me", cancellationToken);
        if (!_sessionStore.TryUpdateProfile(generation, profile))
            throw new OperationCanceledException("Phiên đăng nhập đã thay đổi.");
        return profile;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var session = _sessionStore.Current;
        try
        {
            var refreshToken = session.RefreshToken ?? string.Empty;
            await _apiClient.PostAsync<LogoutRequest>("api/auth/logout", new LogoutRequest { RefreshToken = refreshToken }, cancellationToken);
        }

        finally
        {
            _sessionStore.Clear(session.Generation);
        }
    }
}
