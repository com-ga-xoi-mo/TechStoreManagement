using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Models.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Files;
using TechStore.Client.UnitTests.ViewModels.Catalog;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

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
    public async Task SearchAndStatusFilter_RequestPageOneFromServer()
    {
        var api = new FakeApi(Page(Stock("Phone", "P-1", 12, 0), Stock("Mouse", "M-1", 2, 0)));
        var vm = new InventoryStockViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(1, api.Calls);

        vm.SearchText = "m-1";
        Assert.Equal(2, api.Calls);
        Assert.Equal("m-1", api.LastRequest?.Search);
        Assert.Equal(1, api.LastRequest?.Page);
        Assert.Single(vm.VisibleItems);
        Assert.Equal("Mouse", vm.VisibleItems[0].ProductName);

        vm.SearchText = string.Empty;
        Assert.Equal(3, api.Calls);
        Assert.Null(api.LastRequest?.Search);
        Assert.Equal(1, api.LastRequest?.Page);

        vm.StockStatusFilter = InventoryStockStatusFilter.LowStock;
        Assert.Equal(4, api.Calls);
        Assert.True(api.LastRequest?.LowStock);
        Assert.Equal(1, api.LastRequest?.Page);
        Assert.Single(vm.VisibleItems);
        Assert.Equal("Mouse", vm.VisibleItems[0].ProductName);
    }

    [Fact]
    public async Task ClearingStatusFilter_AfterSinglePageResult_ReloadsUnfiltered()
    {
        var api = new FakeApi(Page(Stock("Phone", "P-1", 12, 0), Stock("Mouse", "M-1", 2, 0)));
        var vm = new InventoryStockViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.VisibleItems.Count);

        vm.StockStatusFilter = InventoryStockStatusFilter.LowStock;
        Assert.Single(vm.VisibleItems);
        Assert.Equal(1, vm.TotalPages);

        vm.StockStatusFilter = InventoryStockStatusFilter.All;
        Assert.Null(api.LastRequest?.LowStock);
        Assert.Equal(1, api.LastRequest?.Page);
        Assert.Equal(2, vm.VisibleItems.Count);
        Assert.Equal(2, vm.TotalCount);
    }

    [Fact]
    public async Task FilterChange_OnLaterPage_RequestsPageOne()
    {
        var api = new FakeApi(Page([Stock("Phone", "P-1", 12, 0), Stock("Mouse", "M-1", 2, 0)], page: 1, pageSize: 1, totalCount: 3, totalPages: 3));
        var vm = new InventoryStockViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.GoToPageCommand.ExecuteAsync(3);
        Assert.Equal(3, vm.Page);

        vm.SearchText = "phone";

        Assert.Equal(1, vm.Page);
        Assert.Equal(1, api.LastRequest?.Page);
        Assert.Equal("phone", api.LastRequest?.Search);
    }

    [Fact]
    public async Task Paging_DoesNotReloadCategoryTree_AndKeepsSelection()
    {
        var cat1Id = Guid.NewGuid();
        var cat2Id = Guid.NewGuid();
        var categories = new CountingCategoryReader(
        [
            new(cat1Id, null, "Điện thoại", "Điện thoại"),
            new(cat2Id, null, "Laptop", "Laptop")
        ]);
        var item1 = new InventoryStockDto { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "iPhone 15", ProductSku = "IP15", CategoryId = cat1Id, CategoryName = "Điện thoại", Quantity = 10, MinStockAlert = 5 };
        var api = new FakeApi(Page([item1], page: 1, pageSize: 1, totalCount: 2, totalPages: 2));
        var vm = new InventoryStockViewModel(api, categoryRepository: categories);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(1, categories.Calls);

        vm.SelectedCategoryId = cat1Id;
        Assert.Equal(1, categories.Calls);
        Assert.Equal(cat1Id, vm.SelectedCategoryId);

        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);
        Assert.Equal(1, categories.Calls);
        Assert.Equal(cat1Id, vm.SelectedCategoryId);
        Assert.Equal(cat1Id, api.LastRequest?.CategoryId);
    }

    [Fact]
    public async Task CategoryAndWarehouseFilters_AreExplicitlyUnavailable()
    {
        var vm = new InventoryStockViewModel(new FakeApi(Page(Stock("Phone", "P-1", 8, 0))));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsCategoryFilterAvailable);
        Assert.False(vm.IsWarehouseFilterAvailable);
        vm.SelectedCategoryId = Guid.NewGuid();
        vm.SelectedWarehouse = "Kho Cửa hàng chính";
        Assert.Single(vm.VisibleItems);
    }

    [Fact]
    public async Task CategoryFilter_TwoCategoriesWithSameName_FiltersByCategoryId()
    {
        var cat1Id = Guid.NewGuid();
        var cat2Id = Guid.NewGuid();
        var categories = new StubCategoryReader
        {
            CustomOptions =
            [
                new(cat1Id, null, "Phụ kiện", "Điện thoại / Phụ kiện"),
                new(cat2Id, null, "Phụ kiện", "Laptop / Phụ kiện")
            ]
        };
        var item1 = new InventoryStockDto { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "Ốp lưng", ProductSku = "CASE-1", CategoryId = cat1Id, CategoryName = "Phụ kiện", Quantity = 10 };
        var item2 = new InventoryStockDto { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "Đế tản nhiệt", ProductSku = "COOL-1", CategoryId = cat2Id, CategoryName = "Phụ kiện", Quantity = 10 };
        var api = new FakeApi(Page(item1, item2));
        var vm = new InventoryStockViewModel(api, categoryRepository: categories);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsCategoryFilterAvailable);
        Assert.Equal(3, vm.CategoryOptions.Count);
        vm.SelectedCategoryId = cat2Id;

        var visible = Assert.Single(vm.VisibleItems);
        Assert.Equal("Đế tản nhiệt", visible.ProductName);
        Assert.Equal(cat2Id, visible.CategoryId);
    }

    [Fact]
    public async Task LowStockFilter_ShowsEveryServerRow_AndKeepsServerTotals()
    {
        var items = Enumerable.Range(1, 15)
            .Select(i => new InventoryStockDto
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductName = $"Item {i}",
                ProductSku = $"SKU-{i}",
                Quantity = 8,
                ReservedQuantity = 0,
                MinStockAlert = 10
            })
            .ToArray();
        var api = new FakeApi(Page(items, page: 1, pageSize: 20, totalCount: 40, totalPages: 2));
        var vm = new InventoryStockViewModel(api)
        {
            StockStatusFilter = InventoryStockStatusFilter.LowStock
        };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(15, vm.VisibleItems.Count);
        Assert.Equal(40, vm.TotalCount);
        Assert.Equal(2, vm.TotalPages);
    }

    [Fact]
    public void StatusFor_UsesRowMinStockAlert()
    {
        var rowMin10 = new InventoryStockRow(new InventoryStockDto { Quantity = 8, MinStockAlert = 10 });
        var rowMin0 = new InventoryStockRow(new InventoryStockDto { Quantity = 5, MinStockAlert = 0 });
        var rowOut = new InventoryStockRow(new InventoryStockDto { Quantity = 0, MinStockAlert = 10 });

        Assert.Equal(InventoryStockStatus.LowStock, rowMin10.Status);
        Assert.Equal(InventoryStockStatus.InStock, rowMin0.Status);
        Assert.Equal(InventoryStockStatus.OutOfStock, rowOut.Status);
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
        var presentation = InventoryStockPresentationRules.StatusFor(available, 5);

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

    [Fact]
    public async Task CancelLoading_DuringLoad_DoesNotApplyResult()
    {
        var pending = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var api = new FakeApi(Page(Stock("Initial", "I-1", 8, 0))) { Handler = _ => pending.Task };
        var vm = new InventoryStockViewModel(api);
        var loading = vm.LoadCommand.ExecuteAsync(null);

        vm.CancelLoading();
        pending.SetResult(Page(Stock("Ignored", "IG-1", 8, 0)));
        await loading;

        Assert.Empty(vm.VisibleItems);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task FilterChange_DuringInitialLoad_CancelsAndReloadsWithFilter()
    {
        var first = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var second = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var api = new FakeApi(Page(Stock("Mouse", "M-1", 2, 0)))
        {
            Handler = call => call == 1 ? first.Task : second.Task
        };
        var vm = new InventoryStockViewModel(api);

        var initialLoad = vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.HasLoaded);
        Assert.True(vm.IsLoading);

        vm.SearchText = "mouse";
        Assert.Equal(2, api.Calls);
        Assert.Equal("mouse", api.LastRequest?.Search);

        second.SetResult(Page(Stock("Mouse", "M-1", 2, 0)));
        first.SetResult(Page(Stock("Phone", "P-1", 12, 0), Stock("Mouse", "M-1", 2, 0)));
        await initialLoad;

        Assert.Equal("Mouse", Assert.Single(vm.VisibleItems).ProductName);
        Assert.True(vm.HasLoaded);
    }

    [Fact]
    public async Task PageButtons_MarkCurrentPage()
    {
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0), page: 2, pageSize: 1, totalCount: 3, totalPages: 3));
        var vm = new InventoryStockViewModel(api) { Page = 2 };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.PageButtons.Count);
        Assert.False(vm.PageButtons[0].IsCurrent);
        Assert.True(vm.PageButtons[1].IsCurrent);
        Assert.False(vm.PageButtons[2].IsCurrent);
    }

    [Fact]
    public async Task ExportReport_FetchesEveryPageWithCurrentFilters()
    {
        // Arrange
        var page1 = Enumerable.Range(1, 100).Select(i => Stock($"P-{i}", $"SKU-{i}", 3, 0)).ToArray();
        var page2 = Enumerable.Range(101, 100).Select(i => Stock($"P-{i}", $"SKU-{i}", 3, 0)).ToArray();
        var page3 = Enumerable.Range(201, 50).Select(i => Stock($"P-{i}", $"SKU-{i}", 3, 0)).ToArray();
        var api = new FakeApi(Page())
        {
            Handler = call => Task.FromResult(call switch
            {
                1 => Page(page1, page: 1, pageSize: 100, totalCount: 250, totalPages: 3),
                2 => Page(page2, page: 2, pageSize: 100, totalCount: 250, totalPages: 3),
                _ => Page(page3, page: 3, pageSize: 100, totalCount: 250, totalPages: 3)
            })
        };
        var saver = new FakeCsvFileSaver { Result = new CsvSaveResult("/tmp/x/bao-cao.csv", false) };
        var vm = new InventoryStockViewModel(api, fileSaver: saver)
        {
            StockStatusFilter = InventoryStockStatusFilter.LowStock
        };

        // Act
        await vm.ExportReportCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(3, api.Requests.Count);
        Assert.All(api.Requests, r =>
        {
            Assert.Equal(100, r.PageSize);
            Assert.True(r.LowStock);
        });
        Assert.Equal([1, 2, 3], api.Requests.Select(r => r.Page ?? 0));
        Assert.Equal(1, saver.Calls);
        Assert.StartsWith("bao-cao-ton-kho-", saver.SuggestedFileName);
        var lines = saver.Content!.TrimEnd('\r', '\n').Split('\n');
        Assert.Equal(251, lines.Length);
        vm.DismissSuccessToast();
    }

    [Fact]
    public async Task ExportReport_Saved_ShowsSuccessToastWithFullPath()
    {
        // Arrange
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0)));
        var saver = new FakeCsvFileSaver { Result = new CsvSaveResult("/tmp/x/bao-cao.csv", false) };
        var vm = new InventoryStockViewModel(api, fileSaver: saver);

        // Act
        await vm.ExportReportCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(ToastKind.Success, vm.ToastKind);
        Assert.Equal("Thành công", vm.ToastTitle);
        Assert.True(vm.IsSuccessToastKind);
        Assert.False(vm.IsErrorToast);
        Assert.Contains("/tmp/x/bao-cao.csv", vm.SuccessToastMessage);
        Assert.False(vm.IsExporting);
        vm.DismissSuccessToast();
    }

    [Fact]
    public async Task ExportReport_FallbackFolder_SaysDialogUnavailable()
    {
        // Arrange
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0)));
        var saver = new FakeCsvFileSaver { Result = new CsvSaveResult("/tmp/x/bao-cao.csv", true) };
        var vm = new InventoryStockViewModel(api, fileSaver: saver);

        // Act
        await vm.ExportReportCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(ToastKind.Success, vm.ToastKind);
        Assert.StartsWith("Không mở được hộp lưu file", vm.SuccessToastMessage);
        Assert.Contains("/tmp/x/bao-cao.csv", vm.SuccessToastMessage);
        vm.DismissSuccessToast();
    }

    [Fact]
    public async Task ExportReport_UserCancels_ShowsNoToast()
    {
        // Arrange
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0)));
        var saver = new FakeCsvFileSaver { Result = null };
        var vm = new InventoryStockViewModel(api, fileSaver: saver);

        // Act
        await vm.ExportReportCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, saver.Calls);
        Assert.False(vm.IsSuccessToastVisible);
    }

    [Fact]
    public async Task ExportReport_NoRows_DoesNotOpenDialog()
    {
        // Arrange
        var api = new FakeApi(Page());
        var saver = new FakeCsvFileSaver();
        var vm = new InventoryStockViewModel(api, fileSaver: saver);

        // Act
        await vm.ExportReportCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, saver.Calls);
        Assert.Equal(ToastKind.Error, vm.ToastKind);
        Assert.Equal("Không thành công", vm.ToastTitle);
        Assert.True(vm.IsErrorToast);
        Assert.True(vm.IsSuccessToastVisible);
        vm.DismissSuccessToast();
    }

    [Fact]
    public async Task ExportReport_ApiFailure_KeepsTableVisible()
    {
        // Arrange
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0)));
        var saver = new FakeCsvFileSaver();
        var vm = new InventoryStockViewModel(api, fileSaver: saver);
        await vm.LoadCommand.ExecuteAsync(null);
        api.Handler = _ => Task.FromException<PagedResultDto<InventoryStockDto>>(
            new ApiException(ApiErrorKind.Network, "Network error"));

        // Act
        await vm.ExportReportCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Single(vm.VisibleItems);
        Assert.Equal(ToastKind.Error, vm.ToastKind);
        Assert.Equal("Không thành công", vm.ToastTitle);
        vm.DismissSuccessToast();
    }

    [Fact]
    public void ExportReport_WithoutFileSaver_CannotExecute()
    {
        // Arrange & Act
        var vm = new InventoryStockViewModel(new FakeApi(Page()));

        // Assert
        Assert.False(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportButtonText_ReflectsExporting()
    {
        // Arrange
        var tcs = new TaskCompletionSource<CsvSaveResult?>();
        var api = new FakeApi(Page(Stock("Phone", "P-1", 8, 0)));
        var saver = new FakeCsvFileSaver { Handler = _ => tcs.Task };
        var vm = new InventoryStockViewModel(api, fileSaver: saver);

        // Act
        var exportTask = vm.ExportReportCommand.ExecuteAsync(null);

        // Assert while exporting
        Assert.Equal("Đang xuất...", vm.ExportButtonText);
        Assert.False(vm.ExportReportCommand.CanExecute(null));

        tcs.SetResult(null);
        await exportTask;

        Assert.Equal("Xuất báo cáo", vm.ExportButtonText);
        Assert.True(vm.ExportReportCommand.CanExecute(null));
    }

    private static PagedResultDto<InventoryStockDto> Page(params InventoryStockDto[] items) =>
        Page(items, 1, 20, items.Length, items.Length == 0 ? 0 : 1);

    private static PagedResultDto<InventoryStockDto> Page(
        InventoryStockDto item, int page, int pageSize, int totalCount, int totalPages) =>
        Page([item], page, pageSize, totalCount, totalPages);

    private static PagedResultDto<InventoryStockDto> Page(
        IReadOnlyList<InventoryStockDto> items, int page, int pageSize, int totalCount, int totalPages) =>
        new() { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount, TotalPages = totalPages };

    private static InventoryStockDto Stock(string name, string sku, int quantity, int reserved, int minStockAlert = 5) => new()
    {
        Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = name, ProductSku = sku,
        Quantity = quantity, ReservedQuantity = reserved, MinStockAlert = minStockAlert
    };

    private sealed class CountingCategoryReader(IReadOnlyList<CategoryOption> options) : TechStore.Client.Repositories.Catalog.ICategoryRepository
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<CategoryOption>> GetTreeAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(options);
        }
    }

    private sealed class FakeCsvFileSaver : ICsvFileSaver
    {
        public int Calls { get; private set; }
        public string? SuggestedFileName { get; private set; }
        public string? Content { get; private set; }
        public CsvSaveResult? Result { get; set; }
        public Exception? Throw { get; set; }
        public Func<CancellationToken, Task<CsvSaveResult?>>? Handler { get; set; }

        public Task<CsvSaveResult?> SaveAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default)
        {
            Calls++;
            SuggestedFileName = suggestedFileName;
            Content = content;
            if (Throw is not null) return Task.FromException<CsvSaveResult?>(Throw);
            if (Handler is not null) return Handler(cancellationToken);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeApi(PagedResultDto<InventoryStockDto> page) : IInventoryRepository
    {
        public int Calls { get; private set; }
        public List<InventoryStockFilterRequest> Requests { get; } = [];
        public InventoryStockFilterRequest? LastRequest { get; private set; }
        public Func<int, Task<PagedResultDto<InventoryStockDto>>>? Handler { get; set; }
        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Requests.Add(request);
            LastRequest = request;
            if (Handler is not null) return Handler(Calls);

            IEnumerable<InventoryStockDto> items = page.Items;
            var filtered = false;
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                filtered = true;
                items = items.Where(i =>
                    i.ProductName.Contains(request.Search, StringComparison.OrdinalIgnoreCase)
                    || i.ProductSku.Contains(request.Search, StringComparison.OrdinalIgnoreCase)
                    || (i.VariantName?.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (i.VariantSku?.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            if (request.CategoryId is Guid catId)
            {
                filtered = true;
                items = items.Where(i => i.CategoryId == catId);
            }
            if (request.LowStock == true)
            {
                filtered = true;
                items = items.Where(i => i.AvailableQuantity > 0 && i.AvailableQuantity <= i.MinStockAlert);
            }
            if (request.OutOfStock == true)
            {
                filtered = true;
                items = items.Where(i => i.AvailableQuantity <= 0);
            }
            if (request.InStock == true)
            {
                filtered = true;
                items = items.Where(i => i.AvailableQuantity > i.MinStockAlert);
            }
            var list = items.ToList();
            var hasMultiPageMetadata = page.TotalCount > page.Items.Count || page.TotalPages > 1;
            var totalCount = filtered && !hasMultiPageMetadata ? list.Count : page.TotalCount;
            var totalPages = filtered && !hasMultiPageMetadata ? (list.Count == 0 ? 0 : 1) : page.TotalPages;
            return Task.FromResult(new PagedResultDto<InventoryStockDto>
            {
                Items = list,
                Page = request.Page ?? page.Page,
                PageSize = page.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }
    }
}
