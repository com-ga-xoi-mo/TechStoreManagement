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
