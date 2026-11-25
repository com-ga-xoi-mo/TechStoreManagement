namespace TechStore.Client.Services.Navigation;

public static class RoleAccess
{
    public static bool CanManageCategories(string? role) => role == "Admin";
    public static bool CanManageStock(string? role) => role is "Admin" or "WarehouseStaff";
    public static bool CanSettleReturns(string? role) => role is "Admin" or "Manager";
    public static bool CanManageOrders(string? role) => role is "Admin" or "SalesStaff";
    public static bool CanManageCatalog(string? role) => role is "Admin" or "Manager";
    public static bool CanEditStoreSettings(string? role) => role is "Admin" or "Manager";
    public static bool CanCreateCustomers(string? role) => role is "Admin" or "Manager" or "SalesStaff";
    public static bool CanManageCustomers(string? role) => role is "Admin" or "Manager";
    public static bool CanManageVouchers(string? role) => role is "Admin" or "Manager";
    public static bool CanReconcileVietQr(string? role) => role is "Admin" or "Manager";

    public static bool CanReceiveWarranty(string? role) => role is "Admin" or "Manager" or "SalesStaff";
    public static bool CanInspectWarranty(string? role) => CanManageStock(role);
    public static bool CanDecideWarrantyReplacement(string? role) => CanSettleReturns(role);

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
            ShellDestination.Assistant => role is "Admin" or "Manager" or "SalesStaff" or "WarehouseStaff",
            ShellDestination.Products => role is "Admin" or "Manager" or "SalesStaff" or "WarehouseStaff",
            ShellDestination.Inventory => role is "Admin" or "Manager" or "WarehouseStaff",
            ShellDestination.ReturnInspections => CanManageStock(role),
            ShellDestination.Warranty => role is "Admin" or "Manager" or "SalesStaff" or "WarehouseStaff",
            ShellDestination.Orders => role is "Admin" or "Manager" or "SalesStaff",
            _ => false
        },
        AppRouteKind.ProductList or AppRouteKind.ProductDetail => CanOpen(AppRoute.Root(ShellDestination.Products), role),
        AppRouteKind.ProductCreate or AppRouteKind.ProductEdit => CanManageCatalog(role),
        AppRouteKind.OrderReturns => CanOpen(AppRoute.Root(ShellDestination.Orders), role),
        AppRouteKind.OrderCreate or AppRouteKind.Pos => CanManageOrders(role),
        AppRouteKind.UserManagement => role == "Admin",
        AppRouteKind.VoucherList => CanManageVouchers(role),
        AppRouteKind.VietQrReconcile => CanReconcileVietQr(role),
        _ => false
    };

    public static void EnsureCanOpen(AppRoute route, string? role)
    {
        if (!CanOpen(route, role)) throw new UnauthorizedAccessException("Bạn không có quyền mở trang này.");
    }
}
