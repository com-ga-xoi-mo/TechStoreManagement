using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.Views.Catalog;

public sealed partial class ProductDetailDialog : ContentDialog
{
    public ProductDetailViewModel ViewModel { get; }

    public ProductDetailDialog(ProductDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }
}
