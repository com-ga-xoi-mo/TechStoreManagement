using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Shell;
using System.Linq;

namespace TechStore.Client.Views.Shell;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        this.InitializeComponent();
        if (NavView.SettingsItem is NavigationViewItem settingsItem)
        {
            settingsItem.Content = "Cài đặt";
        }
        
        // Setup Frame Adapter
        var navService = App.Services.GetRequiredService<INavigationService>();
        navService.SetFrameAdapter(new UnoFrameAdapter(ContentFrame));
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ViewModel.ShowSettingsDialogCommand.Execute(null);
            return;
        }

        if (args.InvokedItemContainer?.Tag is RouteId routeId)
        {
            ViewModel.Navigate(routeId);
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        var navService = App.Services.GetRequiredService<INavigationService>();
        navService.GoBack();
    }
}
