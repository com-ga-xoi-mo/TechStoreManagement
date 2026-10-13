using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.Views.Catalog;

public sealed partial class ProductEditDialog : ContentDialog
{
    public ProductEditViewModel ViewModel { get; }

    public ProductEditDialog(ProductEditViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Closed += OnDialogClosed;
        Closing += OnDialogClosing;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (!ViewModel.CanEdit)
        {
            return;
        }

        await ViewModel.SubmitAsync();
        if (ViewModel.IsSaved)
        {
            Hide();
            return;
        }

        await FocusFirstInvalidFieldAsync();
    }

    private void OnDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        if (!ViewModel.IsSaved)
        {
            ViewModel.Cancel();
        }

        ViewModel.CancelLoad();
    }

    private void OnDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (ViewModel.IsSaving)
        {
            args.Cancel = true;
        }
    }

    private void OnProductImageFailed(object sender, ExceptionRoutedEventArgs args) =>
        SetImageFallback();

    private void OnProductImageOpened(object sender, RoutedEventArgs args) =>
        ProductImage.Visibility = Visibility.Visible;

    private void OnImageUrlTextChanged(object sender, TextChangedEventArgs args) =>
        ProductImage.Visibility = Visibility.Visible;

    private void SetImageFallback()
    {
        ProductImage.Visibility = Visibility.Collapsed;
        ViewModel.ReportImagePreviewFailure();
    }

    private async System.Threading.Tasks.Task FocusFirstInvalidFieldAsync()
    {
        var field = ViewModel.FirstInvalidFieldName;
        var occurrence = 0;
        var isVariantField = field.StartsWith("Variants[", System.StringComparison.Ordinal);
        if (isVariantField && TryReadIndex(field, "Variants[", out var variantIndex) && variantIndex < ViewModel.Variants.Count)
        {
            ViewModel.SelectedVariant = ViewModel.Variants[variantIndex];
            field = field[(field.IndexOf(']') + 2)..];
        }

        string automationName;
        if (isVariantField && field.StartsWith("Specifications[", System.StringComparison.Ordinal))
        {
            automationName = "Tên thông số kỹ thuật";
            occurrence = ViewModel.Specifications.Count + (TryReadIndex(field, "Specifications[", out var variantSpecIndex) ? variantSpecIndex : 0);
        }
        else if (field.StartsWith("Specifications[", System.StringComparison.Ordinal))
        {
            automationName = "Tên thông số kỹ thuật";
            occurrence = TryReadIndex(field, "Specifications[", out var specIndex) ? specIndex : 0;
        }
        else if (isVariantField)
        {
            automationName = field switch
            {
                "Name" => "Tên biến thể",
                "Sku" => "SKU biến thể",
                "Barcode" => "Barcode biến thể",
                "SellingPriceText" => "Giá bán biến thể",
                "CostPriceText" => "Giá vốn biến thể",
                _ => "Tên biến thể"
            };
        }
        else
        {
            automationName = field switch
            {
                nameof(ProductEditViewModel.Name) => "Tên sản phẩm",
                nameof(ProductEditViewModel.Brand) => "Thương hiệu sản phẩm",
                nameof(ProductEditViewModel.CategoryId) => "Danh mục sản phẩm",
                nameof(ProductEditViewModel.ProductSku) => "SKU sản phẩm",
                nameof(ProductEditViewModel.Barcode) => "Barcode sản phẩm",
                nameof(ProductEditViewModel.ImageUrl) => "Đường dẫn ảnh sản phẩm",
                nameof(ProductEditViewModel.BasePriceText) => "Giá niêm yết",
                nameof(ProductEditViewModel.CostPriceText) => "Giá vốn tham chiếu",
                _ => "Tên sản phẩm"
            };
        }

        await System.Threading.Tasks.Task.Yield();
        var target = FindByAutomationName(this, automationName, ref occurrence);
        target?.Focus(FocusState.Programmatic);
    }

    private static bool TryReadIndex(string value, string prefix, out int index)
    {
        index = -1;
        var start = prefix.Length;
        var end = value.IndexOf(']', start);
        return end > start && int.TryParse(value.AsSpan(start, end - start), out index);
    }

    private static Control? FindByAutomationName(DependencyObject root, string name, ref int occurrence)
    {
        if (root is Control control && AutomationProperties.GetName(control) == name)
        {
            if (occurrence == 0)
            {
                return control;
            }

            occurrence--;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var match = FindByAutomationName(VisualTreeHelper.GetChild(root, index), name, ref occurrence);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
