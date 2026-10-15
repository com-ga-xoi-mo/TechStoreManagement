namespace TechStore.Client.Services.Session;

public interface ISessionStore
{
    SessionSnapshot Current { get; }

    event EventHandler<SessionChangedEventArgs>? SessionChanged;

    void SetAccessToken(string accessToken);

    void SetSession(string accessToken, string refreshToken, string username, string fullName, string role);

    void Clear();

    bool TryExpire(long expectedGeneration);
}
