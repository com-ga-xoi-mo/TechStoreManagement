using System.Collections.Generic;
using System.Linq;

namespace TechStore.Client.Services.Navigation;

public static class NavigationCatalog
{
    private static readonly List<RouteMetadata> _routes = new()
    {
        new RouteMetadata(RouteId.Dashboard, "Tổng quan", "Home"),
        new RouteMetadata(RouteId.Catalog, "Sản phẩm", "Shop", "catalog:read"),
        new RouteMetadata(RouteId.Inventory, "Danh mục & Kho", "AllApps", "inventory:read"),
        new RouteMetadata(RouteId.Orders, "Đơn hàng", "Document", "orders:read"),
        new RouteMetadata(RouteId.Customers, "Khách hàng", "Contact", "customers:read"),
        new RouteMetadata(RouteId.ThemePreview, "Theme Preview", "Highlight", "dev:preview")
    };

    public static IReadOnlyList<RouteMetadata> GetAllRoutes() => _routes.AsReadOnly();

    public static IReadOnlyList<RouteMetadata> GetMenuRoutes() => 
        _routes.Where(r => r.Id != RouteId.ThemePreview).ToList().AsReadOnly();

    public static RouteMetadata GetRoute(RouteId id) => 
        _routes.First(r => r.Id == id);
}
