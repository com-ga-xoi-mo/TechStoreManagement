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
        var response = await _apiClient.PostAsync<LoginRequest, LoginResponseDto>("api/auth/login", request, cancellationToken);
        if (response != null && !string.IsNullOrEmpty(response.AccessToken))
        {
            _sessionStore.SetSession(
                response.AccessToken,
                response.RefreshToken,
                response.User.Username,
                response.User.FullName,
                response.User.Role.ToString()
            );
        }
        return response!;
    }

    public async Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<UserProfileDto>("api/auth/me", cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshToken = _sessionStore.Current.RefreshToken ?? string.Empty;
            await _apiClient.PostAsync<LogoutRequest, object>("api/auth/logout", new LogoutRequest { RefreshToken = refreshToken }, cancellationToken);
        }
        catch
        {
            // Ignore server-side logout exceptions to ensure client session is purged
        }
        finally
        {
            _sessionStore.Clear();
        }
    }
}
