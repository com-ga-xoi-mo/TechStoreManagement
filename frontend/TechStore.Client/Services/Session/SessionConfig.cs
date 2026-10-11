namespace TechStore.Client.Services.Session;

/// <summary>
/// Configuration for the authentication mode.
/// </summary>
public enum AuthMode
{
    /// <summary>
    /// Real authentication is unavailable until the backend auth API is implemented.
    /// </summary>
    Real,

    /// <summary>
    /// Explicit development mode. Opens the empty login form with a fake auth source.
    /// </summary>
    ExplicitPreview
}

public static class SessionConfig
{
    public static AuthMode CurrentMode { get; } = ResolveMode();

    private static AuthMode ResolveMode()
    {
#if DEBUG
        if (string.Equals(
            Environment.GetEnvironmentVariable("TECHSTORE_AUTH_MODE"),
            "preview",
            StringComparison.OrdinalIgnoreCase))
        {
            return AuthMode.ExplicitPreview;
        }
#endif
        return AuthMode.Real;
    }
}

public interface IAuthModeProvider
{
    AuthMode CurrentMode { get; }
}

public sealed class AuthModeProvider(AuthMode currentMode) : IAuthModeProvider
{
    public AuthMode CurrentMode { get; } = currentMode;
}
