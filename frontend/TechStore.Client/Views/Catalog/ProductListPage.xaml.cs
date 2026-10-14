using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Converters;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Views.Catalog;

public sealed partial class ProductListPage : Page, IRoutePage
{
    public ProductListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public ProductListPage(ProductListViewModel viewModel, ApiClientOptions apiOptions)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ((ProductImageSourceConverter)Resources["ProductImageSourceConverter"]).BaseAddress = apiOptions.Validate();
    }

    public void SetRoute(ProductQuery? returnCriteria, Guid? focusProductId) =>
        ViewModel.SetRoute(returnCriteria, focusProductId);

    private void OnProductItemClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: ProductSummary product })
            ViewModel.OpenProductCommand.Execute(product);
    }

    private void OnProductImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        if (sender is Image image) image.Visibility = Visibility.Collapsed;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs args) =>
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

    private void OnPageUnloaded(object sender, RoutedEventArgs args) =>
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (ViewModel.FocusProductId is not null && ViewModel.Snapshot is not null)
            RestoreFocusAfterLoad();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ProductListViewModel.Snapshot))
            DispatcherQueue.TryEnqueue(RestoreFocusAfterLoad);
    }

    private void RestoreFocusAfterLoad()
    {
        if (ViewModel.FocusProductId is not { } focusId) return;
        var product = ViewModel.Products.FirstOrDefault(item => item.Id == focusId);
        if (product is null)
        {
            ListHeading.Focus(FocusState.Programmatic);
            ViewModel.FocusProductId = null;
            return;
        }

        var index = ViewModel.Products.ToList().FindIndex(item => item.Id == product.Id);
        var itemWidth = 258d;
        var spacing = 14d;
        var columns = Math.Max(1, (int)((ProductScrollViewer.ViewportWidth + spacing) / (itemWidth + spacing)));
        var targetOffset = index / columns * (340d + spacing);
        ProductScrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
        if (ProductRepeater.TryGetElement(index) is Button item)
        {
            item.Focus(FocusState.Programmatic);
            ViewModel.FocusProductId = null;
        }
    }

    private void OnProductElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (ViewModel.FocusProductId is not { } focusId
            || args.Element is not Button { DataContext: ProductSummary product }
            || product.Id != focusId)
            return;

        args.Element.Focus(FocusState.Programmatic);
        ViewModel.FocusProductId = null;
    }
}
