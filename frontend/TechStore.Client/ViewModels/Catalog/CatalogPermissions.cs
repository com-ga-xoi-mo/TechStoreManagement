using TechStore.Client.Services.Session;
using TechStore.Client.Services.Navigation;

namespace TechStore.Client.ViewModels.Catalog;

public static class CatalogPermissions
{
    public static bool CanManage(SessionSnapshot session) =>
        session.Status == SessionStatus.Active
        && RoleAccess.CanManageCatalog(session.Role);
}
