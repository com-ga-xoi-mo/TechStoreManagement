namespace TechStore.Client.Services.Session;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly object _gate = new();
    private SessionSnapshot _current = new(0, SessionStatus.Anonymous, null);

    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void SetAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        SessionChangedEventArgs notification;
        lock (_gate)
        {
            _current = new(_current.Generation + 1, SessionStatus.Active, accessToken);
            notification = new(_current.Generation, _current.Status);
        }

        SessionChanged?.Invoke(this, notification);
    }

    public void Clear()
    {
        SessionChangedEventArgs notification;
        lock (_gate)
        {
            _current = new(_current.Generation + 1, SessionStatus.Anonymous, null);
            notification = new(_current.Generation, _current.Status);
        }

        SessionChanged?.Invoke(this, notification);
    }

    public bool TryExpire(long expectedGeneration)
    {
        SessionChangedEventArgs notification;
        lock (_gate)
        {
            if (_current.Generation != expectedGeneration || _current.Status != SessionStatus.Active)
            {
                return false;
            }

            _current = new(_current.Generation + 1, SessionStatus.Expired, null);
            notification = new(_current.Generation, _current.Status);
        }

        SessionChanged?.Invoke(this, notification);
        return true;
    }
}
