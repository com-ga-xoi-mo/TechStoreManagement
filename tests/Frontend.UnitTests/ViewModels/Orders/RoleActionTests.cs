using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.ViewModels.Orders;

namespace TechStore.Client.UnitTests.ViewModels.Orders;

public sealed class RoleActionTests
{
    [Theory]
    [InlineData("Admin", true, true, true)]
    [InlineData("Manager", false, false, false)]
    [InlineData("SalesStaff", false, false, true)]
    [InlineData("WarehouseStaff", false, true, false)]
    [InlineData("Unknown", false, false, false)]
    [InlineData(null, false, false, false)]
    public void Actions_RespectSession(string? role, bool categories, bool stock, bool orders)
    {
        var session = new InMemorySessionStore();
        if (role is not null) session.SetSession("token", "refresh", "user", "User", role);
        var inventory = new InventoryStockViewModel(null!, sessionStore: session);
        var serials = new SerialImeiViewModel(null!);
        var workspace = new CategoryManagementViewModel(null!, null!, inventory, serials, null!, null!, session);
        var orderList = new OrderListViewModel(new FakeOrderRepository(), new FakeNavigation(), new FakeDialogService(), new FakeDetailPresenter(), session);
        Assert.Equal(categories, workspace.CanManageCategories);
        Assert.Equal(categories, workspace.ShowCategoryAction);
        Assert.Equal(stock, inventory.CanManageStock);
        Assert.Equal(orders, orderList.CanManageOrders);
    }

    [Fact]
    public void MissingOptionalStore_PreservesGalleryActions()
    {
        var inventory = new InventoryStockViewModel(null!);
        var orders = new OrderListViewModel(new FakeOrderRepository(), new FakeNavigation(), new FakeDialogService(), new FakeDetailPresenter());
        Assert.True(inventory.CanManageStock);
        Assert.True(orders.CanManageOrders);
    }
}
