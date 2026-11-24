using TechStore.Client.Views.Identity;
using TechStore.Client.Services.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Window;
using TechStore.Client.Views.Inventory;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Catalog;
using TechStore.Client.Views.Customers;
using TechStore.Client.Views.Dashboard;
using TechStore.Client.Views.Orders;
using TechStore.Client.Views.Sales;
using TechStore.Client.Views.Settings;
using TechStore.Client.Views.Vouchers;
using TechStore.Client.Views.Warranty;

namespace TechStore.Client.Views.Shell;

public sealed class ShellRoutePageFactory(
    IServiceScopeFactory scopeFactory,
    IWindowLifetime windowLifetime,
    ILogger<ShellRoutePageFactory> logger)
{
    public PreparedRoutePage Create(ShellDestination destination)
        => Create(AppRoute.Root(destination));

    public PreparedRoutePage Create(AppRoute route)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(route);
        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var session = scope.ServiceProvider.GetRequiredService<ISessionStore>().Current;
            RoleAccess.EnsureCanOpen(route, session.Status == SessionStatus.Active ? session.Role : null);
            var page = ResolvePage(route, scope.ServiceProvider);
            if (page is not IRoutePage routePage)
                throw new InvalidOperationException($"The page for {route} does not expose its ViewModel.");

            if (page is OrderReturnsPage returnsPage)
                returnsPage.ViewModel.OrderId = route.OrderId!.Value;
            if (page is PosPage posPage)
                posPage.ViewModel.ExchangeRequestId = route.ExchangeRequestId;
            if (page is ProductListPage listPage)
                listPage.SetRoute(route.ReturnQuery, route.FocusProductId);
            if (page is ProductDetailPage detailPage)
                detailPage.SetRoute(route.ProductId!.Value, route.ReturnQuery, route.FocusProductId);
            if (page is ProductEditorPage editorPage)
                editorPage.SetRoute(route.Kind == AppRouteKind.ProductEdit ? route.ProductId : null, route.ReturnQuery);

            var lease = routePage.ViewModel.CreateActivationLease(
                scope.DisposeAsync,
                windowLifetime.Token);
            return new PreparedRoutePage(
                route,
                page,
                routePage.ViewModel,
                lease,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not create the page for {Route}.", route);
            _ = DisposeFailedScopeAsync(scope);
            throw;
        }
    }

    public void VerifyAllRoutesResolve()
    {
        using var scope = scopeFactory.CreateScope();
        foreach (var destination in ShellDestinations.All)
            _ = ResolvePage(AppRoute.Root(destination.Id), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.Products(), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.ProductDetail(Guid.NewGuid()), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.UserManagement(), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.Vouchers(), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.OrderReturns(Guid.NewGuid()), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.OrderCreate(), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.Pos(), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.ProductCreate(), scope.ServiceProvider);
        _ = ResolvePage(AppRoute.ProductEdit(Guid.NewGuid()), scope.ServiceProvider);
    }

    private static Page ResolvePage(AppRoute route, IServiceProvider services) => route.Kind switch
    {
        AppRouteKind.Root => ResolveRootPage(route.Destination, services),
        AppRouteKind.ProductList => services.GetRequiredService<ProductListPage>(),
        AppRouteKind.ProductDetail => services.GetRequiredService<ProductDetailPage>(),
        AppRouteKind.UserManagement => services.GetRequiredService<UserManagementPage>(),
        AppRouteKind.VoucherList => services.GetRequiredService<VoucherListPage>(),
        AppRouteKind.OrderReturns => services.GetRequiredService<OrderReturnsPage>(),
        AppRouteKind.OrderCreate => services.GetRequiredService<OrderCreatePage>(),
        AppRouteKind.Pos => services.GetRequiredService<PosPage>(),
        AppRouteKind.ProductCreate or AppRouteKind.ProductEdit => services.GetRequiredService<ProductEditorPage>(),
        _ => throw new ArgumentOutOfRangeException(nameof(route), route.Kind, "Unknown route kind.")
    };

    private static Page ResolveRootPage(ShellDestination destination, IServiceProvider services) => destination switch
    {
        ShellDestination.Dashboard => services.GetRequiredService<DashboardPage>(),
        ShellDestination.Products => services.GetRequiredService<ProductListPage>(),
        ShellDestination.Inventory => services.GetRequiredService<CategoryManagementPage>(),
        ShellDestination.Warranty => services.GetRequiredService<WarrantyCaseListPage>(),
        ShellDestination.ReturnInspections => services.GetRequiredService<ReturnInspectionsPage>(),
        ShellDestination.Orders => services.GetRequiredService<OrderListPage>(),
        ShellDestination.Customers => services.GetRequiredService<CustomerListPage>(),
        ShellDestination.Settings => services.GetRequiredService<SettingsPage>(),
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "Unknown Shell destination.")
    };

    private async Task DisposeFailedScopeAsync(AsyncServiceScope scope)
    {
        try
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to dispose a page scope after activation failed.");
        }
    }
}

public sealed class ShellDestinationPreparer(
    ShellRoutePageFactory pageFactory,
    ILogger<ShellDestinationPreparer> logger) : IDestinationPreparer
{
    private PreparedRoutePage? _prepared;

    public bool Prepare(AppRoute route)
    {
        if (_prepared is not null) return false;
        try
        {
            _prepared = pageFactory.Create(route);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Shell route {Route} is unavailable.", route);
            return false;
        }
    }

    public PreparedRoutePage? TakePrepared()
    {
        var prepared = _prepared;
        _prepared = null;
        return prepared;
    }
}
