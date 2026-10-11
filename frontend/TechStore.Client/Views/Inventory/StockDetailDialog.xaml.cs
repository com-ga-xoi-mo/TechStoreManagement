using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Inventory;

namespace TechStore.Client.Views.Inventory;

public sealed partial class StockDetailDialog : ContentDialog
{
    public StockDetailViewModel ViewModel { get; }

    public StockDetailDialog(StockDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
