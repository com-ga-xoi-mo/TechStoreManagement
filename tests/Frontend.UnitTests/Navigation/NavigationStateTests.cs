using TechStore.Client.Services.Navigation;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.UnitTests.Navigation;

public sealed class NavigationStateTests
{
    [Fact]
    public void InitialDestination_IsDashboard_AndRouteMapHasSixDestinations()
    {
        var state = new NavigationState();

        Assert.Equal(ShellDestination.Dashboard, state.CurrentDestination);
        Assert.Equal(6, ShellDestinations.All.Count);
        Assert.Equal("Cài đặt", ShellDestinations.Get(ShellDestination.Settings).Title);
        Assert.True(ShellDestinations.Get(ShellDestination.Settings).IsFooterItem);
        Assert.False(ShellDestinations.TryGet("POS", out _));
    }

    [Fact]
    public void Navigate_CurrentDestination_IsNoOp()
    {
        var state = new NavigationState();
        var prepared = false;

        var outcome = state.TryNavigate(ShellDestination.Dashboard, _ => prepared = true);

        Assert.Equal(NavigationOutcome.AlreadyCurrent, outcome);
        Assert.False(prepared);
        Assert.Equal(ShellDestination.Dashboard, state.CurrentDestination);
    }

    [Fact]
    public void Navigate_UnknownDestination_KeepsCurrentAndDoesNotPrepare()
    {
        var state = new NavigationState();
        var prepared = false;

        var outcome = state.TryNavigate((ShellDestination)999, _ => prepared = true);

        Assert.Equal(NavigationOutcome.UnknownDestination, outcome);
        Assert.False(prepared);
        Assert.Equal(ShellDestination.Dashboard, state.CurrentDestination);
    }

    [Fact]
    public void Navigate_WhenFactoryCannotCreateDestination_KeepsCurrent()
    {
        var state = new NavigationState();

        var outcome = state.TryNavigate(ShellDestination.Products, _ => false);

        Assert.Equal(NavigationOutcome.DestinationUnavailable, outcome);
        Assert.Equal(ShellDestination.Dashboard, state.CurrentDestination);
    }

    [Fact]
    public void Navigate_WhenFactoryCreatesDestination_CommitsOnlyThen()
    {
        var state = new NavigationState();

        var outcome = state.TryNavigate(ShellDestination.Products, destination =>
            destination == ShellDestination.Products);

        Assert.Equal(NavigationOutcome.Navigated, outcome);
        Assert.Equal(ShellDestination.Products, state.CurrentDestination);
    }

    [Fact]
    public void NavigationService_ExposesOnlyDestinationContractToConsumers()
    {
        var preparer = new FakePreparer();
        var service = new NavigationService(preparer);
        ShellDestination? changed = null;
        service.DestinationChanged += (_, destination) => changed = destination;

        var outcome = service.Navigate(ShellDestination.Settings);

        Assert.Equal(NavigationOutcome.Navigated, outcome);
        Assert.Equal(ShellDestination.Settings, service.CurrentDestination);
        Assert.Equal(ShellDestination.Settings, changed);
        Assert.Equal(ShellDestination.Settings, preparer.LastPrepared);
    }

    [Fact]
    public void AppRoute_EqualityUsesProductIdAndValueCriteria()
    {
        var criteria = new ProductQuery { Query = "máy", Page = 3, Specs = ["ram:16"] };
        var first = AppRoute.ProductDetail(Guid.Parse("11111111-1111-1111-1111-111111111111"), criteria, Guid.NewGuid());
        var equal = AppRoute.ProductDetail(first.ProductId!.Value,
            new ProductQuery { Query = "máy", Page = 3, Specs = ["ram:16"] }, first.FocusProductId);
        var differentId = AppRoute.ProductDetail(Guid.NewGuid(), criteria, first.FocusProductId);

        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, differentId);
    }

    [Fact]
    public void AppRoute_RejectsEmptyDetailId()
    {
        Assert.Throws<ArgumentException>(() => AppRoute.ProductDetail(Guid.Empty));
    }

    [Fact]
    public void AppRoute_CopiesReturnCriteriaToKeepRouteValuesImmutable()
    {
        var specs = new List<string> { "ram:16" };
        var route = AppRoute.Products(new ProductQuery { Specs = specs });
        specs[0] = "ram:32";

        Assert.Equal("ram:16", Assert.Single(route.ReturnQuery!.Specs));
    }

    [Fact]
    public void RouteFactoryFailureKeepsCurrentRoute_AndBackCarriesOnlyAppliedCriteria()
    {
        var state = new NavigationState();
        var query = new ProductQuery { Query = "phone", Page = 2, PageSize = 50 };
        Assert.Equal(NavigationOutcome.DestinationUnavailable,
            state.TryNavigate(AppRoute.ProductDetail(Guid.NewGuid(), query), _ => false));
        Assert.Equal(AppRoute.Root(ShellDestination.Dashboard), state.CurrentRoute);

        Assert.Equal(NavigationOutcome.Navigated, state.TryNavigate(AppRoute.Products(query), _ => true));
        var detailId = Guid.NewGuid();
        Assert.Equal(NavigationOutcome.Navigated,
            state.TryNavigate(AppRoute.ProductDetail(detailId, query, detailId), _ => true));
        Assert.Equal(NavigationOutcome.Navigated,
            state.TryNavigate(AppRoute.Products(query, detailId), _ => true));
        Assert.Equal(AppRouteKind.ProductList, state.CurrentRoute.Kind);
        Assert.Equal(query.Query, state.CurrentRoute.ReturnQuery?.Query);
        Assert.Equal(query.Page, state.CurrentRoute.ReturnQuery?.Page);
        Assert.Equal(detailId, state.CurrentRoute.FocusProductId);

        Assert.Equal(NavigationOutcome.Navigated,
            state.TryNavigate(ShellDestination.Settings, _ => true));
        Assert.Equal(AppRoute.Root(ShellDestination.Settings), state.CurrentRoute);
    }

    [Fact]
    public void AppRoute_OrderCreateBelongsToOrdersAndCarriesNoCatalogContext()
    {
        var route = AppRoute.OrderCreate();

        Assert.Equal(ShellDestination.Orders, route.Destination);
        Assert.Equal(AppRouteKind.OrderCreate, route.Kind);
        Assert.Null(route.ProductId);
        Assert.Null(route.ReturnQuery);
        Assert.Null(route.FocusProductId);
    }

    [Fact]
    public void AppRoute_OrderCreateIsEqualToItselfAndDifferentFromTheOrdersRoot()
    {
        var first = AppRoute.OrderCreate();
        var second = AppRoute.OrderCreate();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, AppRoute.Root(ShellDestination.Orders));
    }

    [Fact]
    public void AppRoute_RefusesToBuildAnOrderCreateRouteUnderAnotherDestination()
    {
        var constructor = typeof(AppRoute).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Single();

        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => constructor.Invoke(
            [ShellDestination.Products, AppRouteKind.OrderCreate, null, null, null]));

        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public void NavigationService_MovingBetweenOrdersListAndCreateKeepsTheOrdersDestination()
    {
        var service = new NavigationService(new FakePreparer());
        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(ShellDestination.Orders));
        var destinationChanges = 0;
        var routeChanges = new List<AppRouteKind>();
        service.DestinationChanged += (_, _) => destinationChanges++;
        service.RouteChanged += (_, route) => routeChanges.Add(route.Kind);

        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(AppRoute.OrderCreate()));
        Assert.Equal(ShellDestination.Orders, service.CurrentDestination);
        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(AppRoute.Root(ShellDestination.Orders)));
        Assert.Equal(ShellDestination.Orders, service.CurrentDestination);

        Assert.Equal(0, destinationChanges);
        Assert.Equal([AppRouteKind.OrderCreate, AppRouteKind.Root], routeChanges);
    }

    [Fact]
    public void NavigationService_OpeningOrderCreateAgainWhileOnItIsAlreadyCurrent()
    {
        var service = new NavigationService(new FakePreparer());
        service.Navigate(AppRoute.OrderCreate());

        Assert.Equal(NavigationOutcome.AlreadyCurrent, service.Navigate(AppRoute.OrderCreate()));
    }

    [Fact]
    public void AppRoute_ProductCreateBelongsToProductsAndKeepsReturnQuery()
    {
        var query = new ProductQuery { Query = "phone", Specs = ["ram:16"] };
        var route = AppRoute.ProductCreate(query);

        Assert.Equal(ShellDestination.Products, route.Destination);
        Assert.Equal(AppRouteKind.ProductCreate, route.Kind);
        Assert.Null(route.ProductId);
        Assert.Null(route.FocusProductId);
        Assert.NotNull(route.ReturnQuery);
        Assert.Equal("phone", route.ReturnQuery.Query);
        Assert.Equal("ram:16", Assert.Single(route.ReturnQuery.Specs));
    }

    [Fact]
    public void AppRoute_ProductEditRequiresANonEmptyId()
    {
        Assert.Throws<ArgumentException>(() => AppRoute.ProductEdit(Guid.Empty));
    }

    [Fact]
    public void AppRoute_ProductEditEqualityUsesId()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var query = new ProductQuery { Query = "mouse", Specs = [] };

        var first = AppRoute.ProductEdit(id, query);
        var equal = AppRoute.ProductEdit(id, new ProductQuery { Query = "mouse", Specs = [] });
        var differentId = AppRoute.ProductEdit(otherId, query);
        var detail = AppRoute.ProductDetail(id, query);

        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, differentId);
        Assert.NotEqual(first, detail);
    }

    [Fact]
    public void NavigationService_MovingBetweenProductRoutesKeepsTheProductsDestination()
    {
        var service = new NavigationService(new FakePreparer());
        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(ShellDestination.Products));

        var destinationChanges = 0;
        var routeChanges = new List<AppRouteKind>();
        service.DestinationChanged += (_, _) => destinationChanges++;
        service.RouteChanged += (_, route) => routeChanges.Add(route.Kind);

        var productId = Guid.NewGuid();

        // list -> create -> list -> detail -> edit
        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(AppRoute.ProductCreate()));
        Assert.Equal(ShellDestination.Products, service.CurrentDestination);

        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(AppRoute.Products()));
        Assert.Equal(ShellDestination.Products, service.CurrentDestination);

        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(AppRoute.ProductDetail(productId)));
        Assert.Equal(ShellDestination.Products, service.CurrentDestination);

        Assert.Equal(NavigationOutcome.Navigated, service.Navigate(AppRoute.ProductEdit(productId)));
        Assert.Equal(ShellDestination.Products, service.CurrentDestination);

        Assert.Equal(0, destinationChanges);
        Assert.Equal(
            [AppRouteKind.ProductCreate, AppRouteKind.ProductList, AppRouteKind.ProductDetail, AppRouteKind.ProductEdit],
            routeChanges);
    }

    private sealed class FakePreparer : IDestinationPreparer
    {
        public ShellDestination? LastPrepared { get; private set; }
        public bool Prepare(AppRoute route)
        {
            LastPrepared = route.Destination;
            return true;
        }
    }
}
