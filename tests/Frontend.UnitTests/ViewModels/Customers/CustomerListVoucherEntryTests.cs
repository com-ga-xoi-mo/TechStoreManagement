using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Customers;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Customers;

public sealed class CustomerListVoucherEntryTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("SalesStaff", false)]
    public void CanOpenVouchers_FollowsTheVoucherRole(string role, bool expected)
    {
        // Arrange
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "user", "User", role);
        var viewModel = new CustomerListViewModel(new EmptyCustomers(), session, new RecordingNavigation());

        // Act and Assert
        Assert.Equal(expected, viewModel.CanOpenVouchers);
    }

    [Fact]
    public void CanOpenVouchers_WithoutASession_IsAllowedLikeTheOtherManagementActions()
    {
        // Arrange
        var viewModel = new CustomerListViewModel(new EmptyCustomers(), navigation: new RecordingNavigation());

        // Act and Assert
        Assert.True(viewModel.CanOpenVouchers);
    }

    [Fact]
    public void CanOpenVouchers_WithoutNavigation_IsFalse()
    {
        // Arrange
        var viewModel = new CustomerListViewModel(new EmptyCustomers());

        // Act and Assert
        Assert.False(viewModel.CanOpenVouchers);
        viewModel.OpenVouchersCommand.Execute(null);
    }

    [Fact]
    public void OpenVouchers_Admin_NavigatesToTheVoucherRoute()
    {
        // Arrange
        var navigation = new RecordingNavigation();
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var viewModel = new CustomerListViewModel(new EmptyCustomers(), session, navigation);

        // Act
        viewModel.OpenVouchersCommand.Execute(null);

        // Assert
        Assert.Equal(AppRoute.Vouchers(), Assert.Single(navigation.Navigated));
    }

    [Fact]
    public void OpenVouchers_Sales_DoesNothing()
    {
        // Arrange
        var navigation = new RecordingNavigation();
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "cashier1", "Thu ngân", "SalesStaff");
        var viewModel = new CustomerListViewModel(new EmptyCustomers(), session, navigation);

        // Act
        viewModel.OpenVouchersCommand.Execute(null);

        // Assert
        Assert.Empty(navigation.Navigated);
        Assert.False(viewModel.OpenVouchersCommand.CanExecute(null));
    }

    private sealed class RecordingNavigation : INavigationService
    {
        public List<AppRoute> Navigated { get; } = [];
        public AppRoute CurrentRoute => AppRoute.Root(ShellDestination.Customers);
        public ShellDestination CurrentDestination => ShellDestination.Customers;
        public event EventHandler<ShellDestination>? DestinationChanged { add { } remove { } }
        public event EventHandler<AppRoute>? RouteChanged { add { } remove { } }
        public void Reset(AppRoute route) { }
        public NavigationOutcome Navigate(ShellDestination destination) { Navigated.Add(AppRoute.Root(destination)); return NavigationOutcome.Navigated; }
        public NavigationOutcome Navigate(AppRoute route) { Navigated.Add(route); return NavigationOutcome.Navigated; }
    }

    private sealed class EmptyCustomers : ICustomerRepository
    {
        public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CustomerPage<CustomerSummary>([], 1, 20, 0, 0));
        public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
