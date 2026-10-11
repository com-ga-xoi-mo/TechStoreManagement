using Microsoft.UI.Xaml.Controls;

namespace TechStore.Client.Services.Navigation;

/// <summary>
/// Uno/WinUI Frame adapter that connects our INavigationService to the physical XAML Frame.
/// </summary>
public sealed class UnoFrameAdapter : IFrameAdapter
{
    private readonly Frame _frame;

    public UnoFrameAdapter(Frame frame)
    {
        _frame = frame;
    }

    public bool CanGoBack => _frame.CanGoBack;

    public bool GoBack()
    {
        if (_frame.CanGoBack)
        {
            _frame.GoBack();
            return true;
        }
        return false;
    }

    public bool NavigateToView(RouteId routeId)
    {
        var routeInfo = NavigationCatalog.GetRoute(routeId);

        if (routeId == RouteId.ThemePreview)
        {
            return _frame.Navigate(typeof(Views.Theme.ThemePreviewPage));
        }

        if (routeId == RouteId.Dashboard)
        {
            return _frame.Navigate(typeof(Views.Dashboard.DashboardPage));
        }

        if (routeId == RouteId.Catalog)
        {
            return _frame.Navigate(typeof(Views.Catalog.ProductListPage));
        }

        if (routeId == RouteId.Inventory)
        {
            return _frame.Navigate(typeof(Views.Inventory.InventoryPage));
        }

        // All business routes go to the generic PlaceholderPage for UI-01
        return _frame.Navigate(typeof(Views.Placeholder.PlaceholderPage), routeInfo.Label);
    }
}
