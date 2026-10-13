// HEADLESS CHECK: Verify catalog items and no POS/Identity items
// Run via CLI or test runner when client testing is configured
namespace TechStore.Client.Services.Navigation.Tests
{
    public static class HeadlessNavigationCatalogTests
    {
        public static void Verify()
        {
            var menuRoutes = NavigationCatalog.GetMenuRoutes();
            if (menuRoutes.Count != 5) throw new System.Exception("Expected exactly 5 menu items.");
            
            if (menuRoutes[0].Id != RouteId.Dashboard || menuRoutes[0].Label != "Tổng quan") throw new System.Exception("Invalid Dashboard route.");
            if (menuRoutes[1].Id != RouteId.Catalog || menuRoutes[1].Label != "Sản phẩm") throw new System.Exception("Invalid Catalog route.");
            if (menuRoutes[2].Id != RouteId.Inventory || menuRoutes[2].Label != "Danh mục & Kho") throw new System.Exception("Invalid Inventory route.");
            if (menuRoutes[3].Id != RouteId.Orders || menuRoutes[3].Label != "Đơn hàng") throw new System.Exception("Invalid Orders route.");
            if (menuRoutes[4].Id != RouteId.Customers || menuRoutes[4].Label != "Khách hàng") throw new System.Exception("Invalid Customers route.");
            
            foreach (var route in menuRoutes)
            {
                if (route.Label.Contains("POS") || route.Label.Contains("Identity"))
                {
                    throw new System.Exception("Found disallowed POS/Identity route in menu.");
                }
            }
            
            var allRoutes = NavigationCatalog.GetAllRoutes();
            var uniqueIds = new System.Collections.Generic.HashSet<RouteId>();
            foreach (var route in allRoutes)
            {
                if (!uniqueIds.Add(route.Id)) throw new System.Exception("Duplicate RouteId found.");
            }
        }
    }
}
