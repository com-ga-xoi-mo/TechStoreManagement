using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Views.Orders;

public sealed partial class OrderListPage : Page, IRoutePage
{
    public OrderListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public OrderListPage(OrderListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
