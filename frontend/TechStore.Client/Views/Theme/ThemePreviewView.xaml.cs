using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Theme;

namespace TechStore.Client.Views.Theme;

public sealed partial class ThemePreviewView : UserControl
{
    public ThemePreviewViewModel ViewModel { get; }

    public ThemePreviewView()
    {
        ViewModel = App.Services.GetRequiredService<ThemePreviewViewModel>();
        this.InitializeComponent();
    }
}
