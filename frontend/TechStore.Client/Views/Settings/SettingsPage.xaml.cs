using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Settings;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Views.Settings;

public sealed partial class SettingsPage : Page, IRoutePage
{
    public SettingsViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
