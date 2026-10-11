using Microsoft.UI.Xaml.Controls;

namespace TechStore.Client.Services.Navigation;

/// <summary>
/// Keeps root Frame operations outside ViewModels.
/// </summary>
public sealed class RootNavigationService : IRootNavigationService
{
    public void ShowShell() => Navigate(typeof(Views.Shell.ShellPage));

    public void ShowLogin() => Navigate(typeof(Views.Identity.LoginPage));

    private static void Navigate(Type pageType)
    {
        var window = (Microsoft.UI.Xaml.Application.Current as App)?.GetMainWindow();
        if (window?.Content is Frame rootFrame)
        {
            rootFrame.Navigate(pageType);
            rootFrame.BackStack.Clear();
        }
    }
}
