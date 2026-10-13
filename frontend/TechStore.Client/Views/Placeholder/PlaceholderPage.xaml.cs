using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TechStore.Client.ViewModels.Placeholder;

namespace TechStore.Client.Views.Placeholder;

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderViewModel ViewModel { get; }

    public PlaceholderPage()
    {
        ViewModel = App.Services.GetRequiredService<PlaceholderViewModel>();
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string label)
        {
            ViewModel.Initialize(label);
        }
    }
}
