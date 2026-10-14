using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Views.Customers;

public sealed partial class CustomerListPage : Page, IRoutePage
{
    public CustomerListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public CustomerListPage(CustomerListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
