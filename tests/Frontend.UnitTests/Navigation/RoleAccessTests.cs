using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Shell;
using Xunit.Abstractions;

namespace TechStore.Client.UnitTests.Navigation;

public sealed class RoleAccessTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases =>
    [
        ["Admin", "Dashboard,Products,Inventory,Orders,ReturnInspections,Warranty,Customers,Settings", ShellDestination.Dashboard, true, true, true, true],
        ["Manager", "Dashboard,Products,Inventory,Orders,Warranty,Customers,Settings", ShellDestination.Dashboard, false, false, false, true],
        ["SalesStaff", "Products,Orders,Warranty,Customers,Settings", ShellDestination.Orders, false, false, true, false],
        ["WarehouseStaff", "Products,Inventory,ReturnInspections,Warranty,Settings", ShellDestination.Inventory, false, true, false, false],
        ["Unknown", "Settings", ShellDestination.Settings, false, false, false, false],
        [null!, "Settings", ShellDestination.Settings, false, false, false, false]
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void PolicyAndShell_FollowRoleTable(string? role, string menu, ShellDestination landing, bool categories, bool stock, bool orders, bool catalog)
    {
        var expected = menu.Split(',').Select(value => Enum.Parse<ShellDestination>(value)).ToArray();
        Assert.Equal(expected, RoleAccess.Destinations(role).Select(x => x.Id));
        Assert.Equal(landing, RoleAccess.Landing(role));
        Assert.Equal(categories, RoleAccess.CanManageCategories(role));
        Assert.Equal(stock, RoleAccess.CanManageStock(role));
        Assert.Equal(orders, RoleAccess.CanManageOrders(role));
        Assert.Equal(catalog, RoleAccess.CanManageCatalog(role));
        foreach (var destination in Enum.GetValues<ShellDestination>())
            Assert.Equal(expected.Contains(destination), RoleAccess.CanOpen(AppRoute.Root(destination), role));
        Assert.Equal(expected.Contains(ShellDestination.Products), RoleAccess.CanOpen(AppRoute.Products(), role));
        Assert.Equal(expected.Contains(ShellDestination.Products), RoleAccess.CanOpen(AppRoute.ProductDetail(Guid.NewGuid()), role));
        Assert.Equal(catalog, RoleAccess.CanOpen(AppRoute.ProductCreate(), role));
        Assert.Equal(catalog, RoleAccess.CanOpen(AppRoute.ProductEdit(Guid.NewGuid()), role));
        Assert.Equal(orders, RoleAccess.CanOpen(AppRoute.OrderCreate(), role));
        Assert.Equal(orders, RoleAccess.CanOpen(AppRoute.Pos(), role));
        Assert.Equal(expected.Contains(ShellDestination.Orders), RoleAccess.CanOpen(AppRoute.OrderReturns(Guid.NewGuid()), role));
        Assert.Equal(categories, RoleAccess.CanOpen(AppRoute.UserManagement(), role));

        var store = new InMemorySessionStore();
        var username = role switch { "Admin" => "admin", "Manager" => "manager1", "SalesStaff" => "cashier1", "WarehouseStaff" => "warehouse1", _ => "user" };
        if (role is not null) store.SetSession("token", "refresh", username, username, role);
        var navigation = new NavigationService(new GuardedPreparer(store));
        using var shell = new ShellViewModel(navigation, store);
        output.WriteLine($"user={username} role={role ?? "anonymous"} landing={shell.SelectedDestination} menu={string.Join(",", shell.Destinations.Select(x => x.Id))}");
        Assert.Equal(landing, shell.SelectedDestination);
        Assert.Equal(expected.Where(x => x != ShellDestination.Settings), shell.MainDestinations.Select(x => x.Id));
    }

    [Theory]
    [InlineData("Admin", true, true)]
    [InlineData("Manager", true, true)]
    [InlineData("SalesStaff", true, false)]
    [InlineData("WarehouseStaff", false, false)]
    [InlineData(null, false, false)]
    public void CustomerActions_FollowServerTable(string? role, bool create, bool manage)
    {
        // Act and Assert
        Assert.Equal(create, RoleAccess.CanCreateCustomers(role));
        Assert.Equal(manage, RoleAccess.CanManageCustomers(role));
        Assert.Equal(create, RoleAccess.CanOpen(AppRoute.Root(ShellDestination.Customers), role));
    }

    [Fact]
    public void NewSession_ResetsPreviousRouteWithoutPreparingOrEvents()
    {
        var store = new InMemorySessionStore();
        store.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var preparer = new GuardedPreparer(store);
        var navigation = new NavigationService(preparer);
        using (var admin = new ShellViewModel(navigation, store))
            Assert.Equal(NavigationOutcome.Navigated, navigation.Navigate(ShellDestination.Customers));
        store.Clear();
        store.SetSession("token", "refresh", "sales", "Sales", "SalesStaff");
        var prepared = preparer.Created;
        var events = 0;
        navigation.RouteChanged += (_, _) => events++;
        using var sales = new ShellViewModel(navigation, store);
        Assert.Equal(ShellDestination.Orders, navigation.CurrentDestination);
        Assert.Equal(prepared, preparer.Created);
        Assert.Equal(0, events);
        Assert.Contains(sales.MainDestinations, x => x.Id == ShellDestination.Customers);
    }

    [Theory]
    [InlineData("SalesStaff")]
    [InlineData("WarehouseStaff")]
    [InlineData("Unknown")]
    [InlineData(null)]
    public void Guard_DeniedRouteDoesNotCreatePageOrChangeState(string? role)
    {
        var store = new InMemorySessionStore();
        if (role is not null) store.SetSession("token", "refresh", "user", "User", role);
        var preparer = new GuardedPreparer(store);
        var navigation = new NavigationService(preparer);
        navigation.Reset(AppRoute.Root(ShellDestination.Settings));
        Assert.Equal(NavigationOutcome.DestinationUnavailable, navigation.Navigate(AppRoute.ProductEdit(Guid.NewGuid())));
        Assert.Equal(ShellDestination.Settings, navigation.CurrentDestination);
        Assert.Equal(0, preparer.Created);
    }

    [Theory]
    [InlineData("SalesStaff", ShellDestination.Dashboard)]
    [InlineData("WarehouseStaff", ShellDestination.Dashboard)]
    [InlineData("WarehouseStaff", ShellDestination.Customers)]
    [InlineData("SalesStaff", ShellDestination.Inventory)]
    [InlineData("WarehouseStaff", ShellDestination.Orders)]
    [InlineData("Unknown", ShellDestination.Products)]
    [InlineData(null, ShellDestination.Dashboard)]
    public void Guard_DeniedRootPreservesCurrentPage(string? role, ShellDestination destination)
    {
        var store = new InMemorySessionStore();
        if (role is not null) store.SetSession("token", "refresh", "user", "User", role);
        var preparer = new GuardedPreparer(store);
        var navigation = new NavigationService(preparer);
        navigation.Reset(AppRoute.Root(ShellDestination.Settings));
        Assert.Equal(NavigationOutcome.DestinationUnavailable, navigation.Navigate(destination));
        Assert.Equal(ShellDestination.Settings, navigation.CurrentDestination);
        Assert.Equal(0, preparer.Created);
    }

    private sealed class GuardedPreparer(ISessionStore store) : IDestinationPreparer
    {
        public int Created { get; private set; }
        public bool Prepare(AppRoute route)
        {
            try
            {
                var session = store.Current;
                RoleAccess.EnsureCanOpen(route, session.Status == SessionStatus.Active ? session.Role : null);
                Created++;
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
