using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.Converters;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Views.Catalog;

public sealed partial class ProductDetailPage : Page, IRoutePage
{
    public ProductDetailViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public ProductDetailPage(ProductDetailViewModel viewModel, ApiClientOptions apiOptions)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ((ProductImageSourceConverter)Resources["ProductImageSourceConverter"]).BaseAddress = apiOptions.Validate();
    }

    public void SetRoute(Guid productId, ProductQuery? returnCriteria, Guid? focusProductId) =>
        ViewModel.SetRoute(productId, returnCriteria, focusProductId);

    private void OnPageLoaded(object sender, RoutedEventArgs args) => DetailHeading.Focus(FocusState.Programmatic);

    private void OnProductImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        if (sender is Image image) image.Visibility = Visibility.Collapsed;
    }
}
