using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Inventory;

// Mirrors the server roles of docs/API_PERMISSIONS.md so the UI hides actions the API would refuse.
public static class InventoryPermissions
{
    // Receipts, suppliers and device status changes: the stock roles (Admin and WarehouseStaff).
    public static bool CanWrite(SessionSnapshot session) =>
        session.Status == SessionStatus.Active && RoleAccess.CanManageStock(session.Role);

    // Receipt and supplier lists: Admin, Manager and WarehouseStaff.
    public static bool CanReadPurchases(SessionSnapshot session) =>
        CanWrite(session)
        || (session.Status == SessionStatus.Active
            && string.Equals(session.Role, "Manager", StringComparison.Ordinal));
}
