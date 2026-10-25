using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Features.Inventory.ViewModels;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Features.Inventory;

public sealed class InventoryStockViewModelTests
{
    [Fact]
    public async Task Load_DerivesStatusAndQuantitiesWithoutFabricatingPriceOrCategory()
    {
        var api = new FakeApi(Page(
            Stock("Phone", "P-1", quantity: 12, reserved: 2),
            Stock("Mouse", "M-1", quantity: 5, reserved: 1),
            Stock("Laptop", "L-1", quantity: 0, reserved: 0)));
        var vm = new InventoryStockViewModel(api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Còn hàng", "Sắp hết hàng", "Hết hàng"], vm.VisibleItems.Select(x => x.StatusText));
        Assert.Equal("10 chiếc", vm.VisibleItems[0].AvailableText);
        Assert.Equal("—", vm.VisibleItems[0].PriceText);
        Assert.Equal("—", vm.VisibleItems[0].CategoryName);
        Assert.True(vm.VisibleItems[2].IsDimmed);
    }

    [Fact]
    public async Task Load_UsesCategoryAndSellingPriceFromApiContract()
    {
        var api = new FakeApi(Page(new InventoryStockDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ProductName = "iPhone 15",
            ProductSku = "IP15",
            CategoryName = "Điện thoại",
            SellingPrice = 24_990_000m,
            Quantity = 8
        }));
        var vm = new InventoryStockViewModel(api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Điện thoại", vm.VisibleItems[0].CategoryName);
        Assert.Equal("24.990.000 ₫", vm.VisibleItems[0].PriceText);
    }

    [Fact]
    public async Task SearchAndStatusFilter_AreImmediateAndDoNotCallApi()
    {
        var api = new FakeApi(Page(Stock("Phone", "P-1", 12, 0), Stock("Mouse", "M-1", 2, 0)));
        var vm = new InventoryStockViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);
        var calls = api.Calls;

        vm.SearchText = "m-1";
        Assert.Single(vm.VisibleItems);
        Assert.Equal("Mouse", vm.VisibleItems[0].ProductName);
        vm.SearchText = string.Empty;
        vm.StockStatusFilter = InventoryStockStatusFilter.LowStock;

        Assert.Single(vm.VisibleItems);
        Assert.Equal("Mouse", vm.VisibleItems[0].ProductName);
        Assert.Equal(calls, api.Calls);
    }

    [Fact]
    public async Task CategoryAndWarehouseFilters_AreExplicitlyUnavailable()
    {
        var vm = new InventoryStockViewModel(new FakeApi(Page(Stock("Phone", "P-1", 8, 0))));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsCategoryFilterAvailable);
        Assert.False(vm.IsWarehouseFilterAvailable);
        vm.SelectedCategory = "Laptop";
        vm.SelectedWarehouse = "Kho Cửa hàng chính";
        Assert.Single(vm.VisibleItems);
    }

    [Fact]
    public async Task PaginationAndRetry_UseServerMetadataAndRecoverFromErrors()
    {
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0), page: 1, pageSize: 1, totalCount: 2, totalPages: 2));
        var vm = new InventoryStockViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.CanGoNext);
        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);
        Assert.Equal(2, api.Calls);
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public async Task DeferredActions_EmitHooksWithoutMutation()
    {
        var vm = new InventoryStockViewModel(new FakeApi(Page(Stock("Phone", "P-1", 8, 0))));
        await vm.LoadCommand.ExecuteAsync(null);
        var actions = new List<InventoryStockAction>();
        vm.ActionRequested += actions.Add;

        vm.ImportStockCommand.Execute(null);
        vm.ViewSerialsCommand.Execute(vm.VisibleItems[0]);
        vm.AdjustStockCommand.Execute(vm.VisibleItems[0]);
        vm.DeleteStockCommand.Execute(vm.VisibleItems[0]);

        Assert.Equal([
            InventoryStockAction.Import,
            InventoryStockAction.ViewSerials,
            InventoryStockAction.Adjust,
            InventoryStockAction.Delete], actions);
        Assert.Single(vm.StockItems);
    }

    [Fact]
    public async Task DeleteAction_EmitsTypedSnapshotWithRowIdentity()
    {
        var vm = new InventoryStockViewModel(new FakeApi(Page(new InventoryStockDto
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProductId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ProductName = "Phone",
            ProductSku = "P-1",
            TotalSerialsCount = 12,
            Quantity = 8
        })));
        await vm.LoadCommand.ExecuteAsync(null);
        InventoryStockDeleteRequest? request = null;
        vm.DeleteRequested += value => request = value;

        vm.DeleteStockCommand.Execute(vm.VisibleItems[0]);

        Assert.NotNull(request);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), request!.StockId);
        Assert.Equal("P-1", request.Sku);
        Assert.Equal(12, request.TotalSerialsCount);
    }

    [Fact]
    public async Task SuccessToast_CanBeDismissedWithoutRetainingPageState()
    {
        var vm = new InventoryStockViewModel(new FakeApi(Page()));
        var showing = vm.ShowSuccessToastAsync("Đã xóa sản phẩm thành công khỏi danh mục tồn kho.");

        Assert.True(vm.IsSuccessToastVisible);
        Assert.Equal("Đã xóa sản phẩm thành công khỏi danh mục tồn kho.", vm.SuccessToastMessage);

        vm.DismissSuccessToast();
        await showing;

        Assert.False(vm.IsSuccessToastVisible);
    }

    [Theory]
    [InlineData(6, InventoryStockStatus.InStock, false)]
    [InlineData(5, InventoryStockStatus.LowStock, false)]
    [InlineData(1, InventoryStockStatus.LowStock, false)]
    [InlineData(0, InventoryStockStatus.OutOfStock, true)]
    [InlineData(-1, InventoryStockStatus.OutOfStock, true)]
    public void PresentationRules_HandleBoundaryAndInvalidAvailability(int available, InventoryStockStatus expected, bool dimmed)
    {
        var presentation = InventoryStockPresentationRules.StatusFor(available);

        Assert.Equal(expected, presentation.Status);
        Assert.Equal(dimmed, presentation.IsDimmed);
    }

    [Fact]
    public async Task Loading_OlderResponseCannotReplaceNewerResponse()
    {
        var first = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var second = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var api = new FakeApi(Page(Stock("Initial", "I-1", 8, 0)))
        {
            Handler = call => call == 1 ? first.Task : second.Task
        };
        var vm = new InventoryStockViewModel(api);

        var oldLoad = vm.LoadCommand.ExecuteAsync(null);
        var newLoad = vm.LoadCommand.ExecuteAsync(null);
        second.SetResult(Page(Stock("New", "N-1", 8, 0)));
        await newLoad;
        first.SetResult(Page(Stock("Old", "O-1", 8, 0)));
        await oldLoad;

        Assert.Equal("New", vm.VisibleItems.Single().ProductName);
        Assert.False(vm.IsLoading);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Loading_CancelledRequestDoesNotBecomeError()
    {
        var pending = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var api = new FakeApi(Page(Stock("Initial", "I-1", 8, 0))) { Handler = _ => pending.Task };
        var vm = new InventoryStockViewModel(api);
        var loading = vm.LoadCommand.ExecuteAsync(null);

        vm.CancelLoading();
        pending.SetResult(Page(Stock("Cancelled", "C-1", 8, 0)));
        await loading;

        Assert.False(vm.IsLoading);
        Assert.False(vm.HasError);
        Assert.Empty(vm.VisibleItems);
    }

    private static PagedResultDto<InventoryStockDto> Page(params InventoryStockDto[] items) =>
        Page(items, 1, 20, items.Length, items.Length == 0 ? 0 : 1);

    private static PagedResultDto<InventoryStockDto> Page(
        InventoryStockDto item, int page, int pageSize, int totalCount, int totalPages) =>
        Page([item], page, pageSize, totalCount, totalPages);

    private static PagedResultDto<InventoryStockDto> Page(
        IReadOnlyList<InventoryStockDto> items, int page, int pageSize, int totalCount, int totalPages) =>
        new() { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount, TotalPages = totalPages };

    private static InventoryStockDto Stock(string name, string sku, int quantity, int reserved) => new()
    {
        Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = name, ProductSku = sku,
        Quantity = quantity, ReservedQuantity = reserved
    };

    private sealed class FakeApi(PagedResultDto<InventoryStockDto> page) : IInventoryApiClient
    {
        public int Calls { get; private set; }
        public Func<int, Task<PagedResultDto<InventoryStockDto>>>? Handler { get; set; }
        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Handler is not null) return Handler(Calls);
            return Task.FromResult(new PagedResultDto<InventoryStockDto>
            {
                Items = page.Items, Page = request.Page ?? page.Page, PageSize = page.PageSize,
                TotalCount = page.TotalCount, TotalPages = page.TotalPages
            });
        }
    }
}
