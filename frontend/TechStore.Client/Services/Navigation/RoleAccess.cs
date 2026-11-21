namespace TechStore.Client.Services.Navigation;

public static class RoleAccess
{
    public static bool CanManageCategories(string? role) => role == "Admin";
    public static bool CanManageStock(string? role) => role is "Admin" or "WarehouseStaff";
    public static bool CanManageOrders(string? role) => role is "Admin" or "SalesStaff";
    public static bool CanManageCatalog(string? role) => role is "Admin" or "Manager";
    public static bool CanEditStoreSettings(string? role) => role is "Admin" or "Manager";
    public static bool CanCreateCustomers(string? role) => role is "Admin" or "Manager" or "SalesStaff";
    public static bool CanManageCustomers(string? role) => role is "Admin" or "Manager";

    public static IReadOnlyList<ShellDestinationInfo> Destinations(string? role) =>
        ShellDestinations.All.Where(item => CanOpen(AppRoute.Root(item.Id), role)).ToArray();

    public static ShellDestination Landing(string? role) =>
        new[] { ShellDestination.Dashboard, ShellDestination.Orders, ShellDestination.Inventory, ShellDestination.Products }
            .FirstOrDefault(destination => CanOpen(AppRoute.Root(destination), role), ShellDestination.Settings);

    public static bool CanOpen(AppRoute route, string? role) => route.Kind switch
    {
        AppRouteKind.Root => route.Destination switch
        {
            ShellDestination.Settings => true,
            ShellDestination.Dashboard => role is "Admin" or "Manager",
            ShellDestination.Customers => CanCreateCustomers(role),
            ShellDestination.Products => role is "Admin" or "Manager" or "SalesStaff" or "WarehouseStaff",
            ShellDestination.Inventory => role is "Admin" or "Manager" or "WarehouseStaff",
            ShellDestination.Orders => role is "Admin" or "Manager" or "SalesStaff",
            _ => false
        },
        AppRouteKind.ProductList or AppRouteKind.ProductDetail => CanOpen(AppRoute.Root(ShellDestination.Products), role),
        AppRouteKind.ProductCreate or AppRouteKind.ProductEdit => CanManageCatalog(role),
        AppRouteKind.OrderCreate or AppRouteKind.Pos => CanManageOrders(role),
        AppRouteKind.UserManagement => role == "Admin",
        _ => false
    };

    public static void EnsureCanOpen(AppRoute route, string? role)
    {
        if (!CanOpen(route, role)) throw new UnauthorizedAccessException("Bạn không có quyền mở trang này.");
    }
}
