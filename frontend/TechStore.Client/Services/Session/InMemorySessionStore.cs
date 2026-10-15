namespace TechStore.Client.Services.Session;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly object _gate = new();
    private readonly ISessionStorage? _storage;
    private SessionSnapshot _current;

    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public InMemorySessionStore(ISessionStorage? storage = null)
    {
        _storage = storage;
        var persisted = _storage?.Load();
        if (persisted != null && !string.IsNullOrWhiteSpace(persisted.AccessToken))
        {
            _current = new(1, SessionStatus.Active, persisted.AccessToken, persisted.RefreshToken, persisted.Username, persisted.FullName, persisted.Role);
        }
        else
        {
            _current = new(0, SessionStatus.Anonymous, null);
        }
    }

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

    public void SetSession(string accessToken, string refreshToken, string username, string fullName, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        SessionChangedEventArgs notification;
        lock (_gate)
        {
            _current = new(_current.Generation + 1, SessionStatus.Active, accessToken, refreshToken, username, fullName, role);
            notification = new(_current.Generation, _current.Status);
        }

        _storage?.Save(new PersistedSessionData(accessToken, refreshToken, username, fullName, role));
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

        _storage?.Clear();
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

        _storage?.Clear();
        SessionChanged?.Invoke(this, notification);
        return true;
    }
}
