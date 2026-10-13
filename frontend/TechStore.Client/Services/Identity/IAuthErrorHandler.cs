using System;
using TechStore.Client.Services.Session;

namespace TechStore.Client.Services.Identity;

/// <summary>
/// A seam for handling protected request errors and session expiry.
/// For UI-02, this only simulates responses to 401/403.
/// Real HTTP interception requires a protected endpoint contract in UI-0X.
/// </summary>
public interface IAuthErrorHandler
{
    void HandleUnauthorized();
    void HandleForbidden();
}

public sealed class AuthErrorHandler : IAuthErrorHandler
{
    private readonly ICurrentSession _session;

    public AuthErrorHandler(ICurrentSession session)
    {
        _session = session;
    }

    public void HandleUnauthorized()
    {
        // 401 Unrecoverable (e.g. token expired and refresh failed) -> Clear session
        _session.ClearSession();
        
        // Return to login
        var window = (Microsoft.UI.Xaml.Application.Current as TechStore.Client.App)?.GetMainWindow();
        if (window?.Content is Microsoft.UI.Xaml.Controls.Frame rootFrame)
        {
            rootFrame.BackStack.Clear();
            rootFrame.Navigate(typeof(TechStore.Client.Views.Identity.LoginPage));
        }
    }

    public void HandleForbidden()
    {
        // 403 Forbidden -> User is authenticated but lacks roles. 
        // Session is preserved, but we might show a dialog or navigate to a forbidden page.
        // For now, no-op or trigger a local event.
    }
}
