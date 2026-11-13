using TechStore.Shared.DTOs;

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
        var saved = storage?.Load();
        _current = saved is { RememberMe: true } && !string.IsNullOrWhiteSpace(saved.AccessToken)
            ? new(1, SessionStatus.Active, saved.AccessToken, saved.RefreshToken, saved.Username, saved.FullName, saved.Role, saved.ExpiresAt)
            : new(0, SessionStatus.Anonymous, null);
    }

    public SessionSnapshot Current { get { lock (_gate) return _current; } }

    public void SetAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        SessionChangedEventArgs notification;
        lock (_gate)
        {
            _storage?.Clear();
            _current = new(_current.Generation + 1, SessionStatus.Active, accessToken, RememberMe: false);
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
            _storage?.Save(new(accessToken, refreshToken, username, fullName, role));
            _current = new(_current.Generation + 1, SessionStatus.Active, accessToken, refreshToken, username, fullName, role);
            notification = new(_current.Generation, _current.Status);
        }
        SessionChanged?.Invoke(this, notification);
    }

    public bool TrySetSession(long expectedGeneration, LoginResponseDto response, bool rememberMe, bool renewal = false, string? expectedAccessToken = null, string? expectedRefreshToken = null)
    {
        if (string.IsNullOrWhiteSpace(response.AccessToken) || string.IsNullOrWhiteSpace(response.RefreshToken)
            || response.User is null || response.ExpiresIn <= 0)
            throw new InvalidOperationException("Phản hồi đăng nhập không hợp lệ.");
        SessionChangedEventArgs? notification = null;
        lock (_gate)
        {
            if (_current.Generation != expectedGeneration || (renewal && (_current.Status != SessionStatus.Active || _current.AccessToken != expectedAccessToken || _current.RefreshToken != expectedRefreshToken))) return false;
            var profile = response.User;
            var expires = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            if (rememberMe) _storage?.Save(new(response.AccessToken, response.RefreshToken, profile.Username, profile.FullName, profile.Role.ToString(), expires));
            else _storage?.Clear();
            // Renewal keeps the account generation and page state; 401 also compares the access token.
            _current = new(renewal ? _current.Generation : _current.Generation + 1, SessionStatus.Active,
                response.AccessToken, response.RefreshToken, profile.Username, profile.FullName, profile.Role.ToString(), expires, rememberMe);
            if (!renewal) notification = new(_current.Generation, _current.Status);
        }
        if (notification is not null) SessionChanged?.Invoke(this, notification);
        return true;
    }

    public bool TryUpdateProfile(long expectedGeneration, UserProfileDto profile)
    {
        lock (_gate)
        {
            if (_current.Generation != expectedGeneration || _current.Status != SessionStatus.Active) return false;
            var updated = _current with { Username = profile.Username, FullName = profile.FullName, Role = profile.Role.ToString() };
            if (updated.RememberMe) _storage?.Save(new(updated.AccessToken!, updated.RefreshToken!, updated.Username!, updated.FullName!, updated.Role!, updated.ExpiresAt));
            _current = updated;
            return true;
        }
    }

    public void Clear(long? expectedGeneration = null)
    {
        SessionChangedEventArgs? notification = null;
        try
        {
            lock (_gate)
            {
                if (expectedGeneration is not null && _current.Generation != expectedGeneration) return;
                _current = new(_current.Generation + 1, SessionStatus.Anonymous, null);
                notification = new(_current.Generation, _current.Status);
                _storage?.Clear();
            }
        }
        finally { if (notification is not null) SessionChanged?.Invoke(this, notification); }
    }

    public bool TryExpire(long expectedGeneration, string? expectedAccessToken = null)
    {
        SessionChangedEventArgs? notification = null;
        try
        {
            lock (_gate)
            {
                if (_current.Generation != expectedGeneration || _current.Status != SessionStatus.Active
                    || (expectedAccessToken is not null && _current.AccessToken != expectedAccessToken)) return false;
                _current = new(_current.Generation + 1, SessionStatus.Expired, null);
                notification = new(_current.Generation, _current.Status);
                _storage?.Clear();
            }
            return true;
        }
        finally { if (notification is not null) SessionChanged?.Invoke(this, notification); }
    }
}
