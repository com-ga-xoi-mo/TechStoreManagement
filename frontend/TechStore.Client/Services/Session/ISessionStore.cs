using TechStore.Shared.DTOs;

namespace TechStore.Client.Services.Session;

public interface ISessionStore
{
    SessionSnapshot Current { get; }

    event EventHandler<SessionChangedEventArgs>? SessionChanged;

    void SetAccessToken(string accessToken);

    void SetSession(string accessToken, string refreshToken, string username, string fullName, string role);

    void Clear(long? expectedGeneration = null);

    bool TryUpdateProfile(long expectedGeneration, UserProfileDto profile);

    bool TryExpire(long expectedGeneration, string? expectedAccessToken = null);

    bool TrySetSession(long expectedGeneration, LoginResponseDto response, bool rememberMe, bool renewal = false, string? expectedAccessToken = null, string? expectedRefreshToken = null);
}
