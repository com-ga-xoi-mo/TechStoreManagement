using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.Views.Catalog;

public sealed partial class ProductListPage : Page
{
    public ProductListViewModel ViewModel { get; }

    public ProductListPage()
    {
        ViewModel = App.Services.GetRequiredService<ProductListViewModel>();
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo();
        // Avoid repeated initialization if already loaded
        if (ViewModel.Products.Count == 0 && !ViewModel.IsLoading)
        {
            _ = ViewModel.InitializeCommand.ExecuteAsync(null);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }
}
