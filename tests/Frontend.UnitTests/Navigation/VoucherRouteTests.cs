using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;

namespace TechStore.Client.UnitTests.Navigation;

public sealed class VoucherRouteTests
{
    [Fact]
    public void Vouchers_StaysUnderCustomersDestination()
    {
        // Act
        var route = AppRoute.Vouchers();

        // Assert
        Assert.Equal(ShellDestination.Customers, route.Destination);
        Assert.Equal(AppRouteKind.VoucherList, route.Kind);
        Assert.Null(route.ProductId);
        Assert.Null(route.ReturnQuery);
    }

    [Fact]
    public void Vouchers_IsEqualToItselfAndDifferentFromCustomersRoot()
    {
        // Act and Assert
        Assert.Equal(AppRoute.Vouchers(), AppRoute.Vouchers());
        Assert.Equal(AppRoute.Vouchers().GetHashCode(), AppRoute.Vouchers().GetHashCode());
        Assert.NotEqual(AppRoute.Root(ShellDestination.Customers), AppRoute.Vouchers());
    }

    [Fact]
    public void Vouchers_DoesNotAddAMenuEntry()
    {
        // Assert: the page is reached from Customers, so no destination of the menu is about vouchers, for any role.
        Assert.DoesNotContain(ShellDestinations.All, item => item.Title.Contains("Voucher"));
        Assert.DoesNotContain(Enum.GetNames<ShellDestination>(), name => name.Contains("Voucher"));
        foreach (var role in new[] { "Admin", "Manager", "SalesStaff", "WarehouseStaff" })
            Assert.DoesNotContain(RoleAccess.Destinations(role), item => item.Title.Contains("Voucher"));
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("SalesStaff", false)]
    [InlineData("WarehouseStaff", false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void CanManageVouchers_FollowsServerTable_AndRouteFollowsIt(string? role, bool expected)
    {
        // Act and Assert
        Assert.Equal(expected, RoleAccess.CanManageVouchers(role));
        Assert.Equal(expected, RoleAccess.CanOpen(AppRoute.Vouchers(), role));
    }

    [Theory]
    [InlineData("SalesStaff")]
    [InlineData("WarehouseStaff")]
    [InlineData(null)]
    public void Navigate_DeniedRole_KeepsCurrentPageAndCreatesNothing(string? role)
    {
        // Arrange
        var store = new InMemorySessionStore();
        if (role is not null) store.SetSession("token", "refresh", "user", "User", role);
        var preparer = new CountingPreparer(store);
        var navigation = new NavigationService(preparer);
        navigation.Reset(AppRoute.Root(ShellDestination.Settings));

        // Act
        var outcome = navigation.Navigate(AppRoute.Vouchers());

        // Assert
        Assert.Equal(NavigationOutcome.DestinationUnavailable, outcome);
        Assert.Equal(ShellDestination.Settings, navigation.CurrentDestination);
        Assert.Equal(0, preparer.Created);
    }

    [Fact]
    public void Navigate_Manager_OpensVouchersWithCustomersSelected()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetSession("token", "refresh", "manager1", "Manager", "Manager");
        var navigation = new NavigationService(new CountingPreparer(store));
        navigation.Reset(AppRoute.Root(ShellDestination.Customers));

        // Act
        var outcome = navigation.Navigate(AppRoute.Vouchers());

        // Assert
        Assert.Equal(NavigationOutcome.Navigated, outcome);
        Assert.Equal(AppRoute.Vouchers(), navigation.CurrentRoute);
        Assert.Equal(ShellDestination.Customers, navigation.CurrentDestination);
    }

    private sealed class CountingPreparer(ISessionStore store) : IDestinationPreparer
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
