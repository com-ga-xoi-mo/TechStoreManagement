using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Inventory;

namespace TechStore.Client.Views.Inventory;

public sealed partial class StockAdjustmentDialog : ContentDialog
{
    public StockAdjustmentViewModel ViewModel { get; }

    public StockAdjustmentDialog(StockAdjustmentViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
