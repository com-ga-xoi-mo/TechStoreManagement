using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.Views.Catalog;

public sealed partial class ProductCreateDialog : ContentDialog
{
    public ProductCreateViewModel ViewModel { get; }

    public ProductCreateDialog(ProductCreateViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        await ViewModel.SubmitAsync();
        if (ViewModel.IsCreated)
        {
            Hide();
            return;
        }

        FocusFirstInvalidField();
    }

    private void FocusFirstInvalidField()
    {
        var field = ViewModel.FirstInvalidFieldName;
        var (automationName, occurrence) = field switch
        {
            nameof(ProductCreateViewModel.Name) => ("Tên sản phẩm", 0),
            nameof(ProductCreateViewModel.Brand) => ("Hãng sản xuất", 0),
            nameof(ProductCreateViewModel.CategoryId) => ("Danh mục sản phẩm", 0),
            nameof(ProductCreateViewModel.ProductSku) => ("SKU sản phẩm", 0),
            nameof(ProductCreateViewModel.Barcode) => ("Barcode sản phẩm", 0),
            nameof(ProductCreateViewModel.ImageUrl) => ("Đường dẫn ảnh sản phẩm", 0),
            nameof(ProductCreateViewModel.BasePriceText) => ("Giá bán niêm yết", 0),
            nameof(ProductCreateViewModel.CostPriceText) => ("Giá vốn tham chiếu", 0),
            var name when name.StartsWith("Variants[", System.StringComparison.Ordinal) =>
                (VariantAutomationName(name), ParseIndex(name)),
            var name when name.StartsWith("Specifications[", System.StringComparison.Ordinal) =>
                ("Tên thông số phần cứng", ParseIndex(name)),
            _ => ("Tên sản phẩm", 0)
        };

        var target = FindByAutomationName(this, automationName, ref occurrence) ?? NameTextBox;
        target.Focus(FocusState.Programmatic);
    }

    private static string VariantAutomationName(string fieldName)
    {
        if (fieldName.Contains(".Sku", System.StringComparison.Ordinal)) return "SKU biến thể";
        if (fieldName.Contains(".Barcode", System.StringComparison.Ordinal)) return "Barcode biến thể";
        if (fieldName.Contains(".PriceText", System.StringComparison.Ordinal)) return "Giá bán biến thể";
        return fieldName.Contains(".Specifications[", System.StringComparison.Ordinal)
            ? "Tên thông số phần cứng"
            : "Tên biến thể";
    }

    private static int ParseIndex(string fieldName)
    {
        var start = fieldName.IndexOf('[', System.StringComparison.Ordinal);
        var end = fieldName.IndexOf(']', start + 1);
        return start >= 0 && end > start && int.TryParse(fieldName[(start + 1)..end], out var index)
            ? index
            : 0;
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

        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < children; index++)
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
