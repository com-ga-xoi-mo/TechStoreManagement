using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Views.Inventory;

public sealed partial class InventoryPage : Page, IRoutePage
{
    public InventoryViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public InventoryPage(InventoryViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
