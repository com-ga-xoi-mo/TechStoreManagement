using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Catalog;

public static class CatalogPermissions
{
    public static bool CanManage(SessionSnapshot session) =>
        session.Status == SessionStatus.Active
        && (string.Equals(session.Role, "Admin", StringComparison.Ordinal)
            || string.Equals(session.Role, "Manager", StringComparison.Ordinal));
}
