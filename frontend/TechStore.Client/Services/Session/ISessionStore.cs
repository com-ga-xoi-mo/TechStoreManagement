namespace TechStore.Client.Services.Session;

public interface ISessionStore
{
    SessionSnapshot Current { get; }

    event EventHandler<SessionChangedEventArgs>? SessionChanged;

    void SetAccessToken(string accessToken);

    void Clear();

    bool TryExpire(long expectedGeneration);
}
