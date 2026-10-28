using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class ImportStockDialogViewModelTests
{
    [Fact]
    public async Task LoadProducts_MapsActiveProductsToChoices()
    {
        var products = new FakeProducts(new ProductSummary(
            Guid.NewGuid(), "Điện thoại", "PHONE-01", "Brand", Guid.NewGuid(),
            "Điện thoại", 1, 1, 0, false, true, null));
        var vm = new ImportStockDialogViewModel(products, new FakeApi());

        await vm.LoadProductsCommand.ExecuteAsync(null);

        var choice = Assert.Single(vm.ProductOptions);
        Assert.Equal("Điện thoại · PHONE-01", choice.DisplayText);
    }

    [Fact]
    public async Task Confirm_WithoutSerials_InitializesOpeningBalance()
    {
        var product = Option();
        var api = new FakeApi();
        var vm = new ImportStockDialogViewModel(new FakeProducts(), api)
        {
            SelectedProduct = product,
            Quantity = 12,
            UnitCostText = "200.000",
            Reason = "Nhập lô đầu kỳ"
        };
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(12, api.InitializeRequest?.Quantity);
        Assert.Equal(200_000m, api.InitializeRequest?.UnitCost);
        Assert.Equal("Nhập lô đầu kỳ", api.InitializeRequest?.Reason);
        Assert.Equal(1, closed);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Confirm_RequiresSerialCountToMatchQuantity()
    {
        var vm = new ImportStockDialogViewModel(new FakeProducts(), new FakeApi())
        {
            SelectedProduct = Option(),
            Quantity = 2,
            SerialLines = "SN-001"
        };

        Assert.False(vm.CanSubmit);
        Assert.Contains("phải bằng", vm.ValidationMessage);
        await vm.ConfirmCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Confirm_Serials_UsesBatchEndpointAndCloses()
    {
        var api = new FakeApi { SerialImportResult = InventorySerialImportResult.Success(2) };
        var vm = new ImportStockDialogViewModel(new FakeProducts(), api)
        {
            SelectedProduct = Option(),
            Quantity = 2,
            UnitCostText = "21500000",
            SerialLines = "SN-001\n123456789012345"
        };
        var imported = 0;
        vm.ImportSucceeded += count => imported = count;

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(2, imported);
        Assert.Equal(2, api.SerialImportRequest?.Items?.Count);
        Assert.Equal(21_500_000m, api.SerialImportRequest?.DefaultUnitCost);
        Assert.Equal("123456789012345", api.SerialImportRequest?.Items?[1].Imei);
        Assert.Equal(1, api.BatchCalls);
    }

    [Fact]
    public async Task Confirm_ExistingStock_ReportsConflictAndKeepsDialogOpen()
    {
        var api = new FakeApi
        {
            ExistingStocks = new PagedResultDto<InventoryStockDto> { TotalCount = 1 }
        };
        var vm = new ImportStockDialogViewModel(new FakeProducts(), api)
        {
            SelectedProduct = Option(),
            Quantity = 1,
            UnitCostText = "1000",
            SerialLines = "SN-001"
        };
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Contains("đã được khởi tạo", vm.ErrorMessage);
        Assert.Equal(0, closed);
        Assert.Equal(0, api.BatchCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-1")]
    public async Task Confirm_WithoutAValidUnitCost_IsBlockedForGoodsWithoutSerial(string cost)
    {
        var api = new FakeApi();
        var vm = new ImportStockDialogViewModel(new FakeProducts(), api)
        {
            SelectedProduct = Option(),
            Quantity = 3,
            UnitCostText = cost
        };

        Assert.True(vm.NeedsUnitCost);
        Assert.False(vm.CanSubmit);
        Assert.Contains("giá vốn", vm.ValidationMessage);
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Null(api.InitializeRequest);
    }

    [Fact]
    public async Task Confirm_SerialImportWithoutUnitCost_IsBlocked()
    {
        var api = new FakeApi();
        var vm = new ImportStockDialogViewModel(new FakeProducts(), api)
        {
            SelectedProduct = Option(),
            Quantity = 1,
            SerialLines = "SN-001"
        };

        Assert.False(vm.CanSubmit);
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(0, api.BatchCalls);
    }

    [Fact]
    public async Task Confirm_OpeningQuantityOfSerialTrackedGoods_NeedsNoCost()
    {
        var api = new FakeApi();
        var tracked = new ImportStockProductOption(Guid.NewGuid(), "iPhone", "IP15", "Điện thoại", true);
        var vm = new ImportStockDialogViewModel(new FakeProducts(), api)
        {
            SelectedProduct = tracked,
            Quantity = 3,
            UnitCostText = "999"
        };

        Assert.False(vm.NeedsUnitCost);
        Assert.True(vm.CanSubmit);
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(3, api.InitializeRequest?.Quantity);
        Assert.Null(api.InitializeRequest?.UnitCost);
    }

    private static ImportStockProductOption Option() => new(
        Guid.NewGuid(), "Điện thoại", "PHONE-01", "Điện thoại", false);

    private sealed class FakeProducts(params ProductSummary[] products) : IProductRepository
    {
        public Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductPage(products, 1, products.Length, products.Length, products.Length == 0 ? 0 : 1));

        public Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDetail?>(null);
    }

    private sealed class FakeApi : IInventoryRepository
    {
        public InitializeStockRequest? InitializeRequest { get; private set; }
        public BatchImportSerialImeiRequest? SerialImportRequest { get; private set; }
        public int BatchCalls { get; private set; }
        public PagedResultDto<InventoryStockDto> ExistingStocks { get; init; } = new();
        public InventorySerialImportResult SerialImportResult { get; init; } = InventorySerialImportResult.Success(0);

        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingStocks);

        public Task<InventoryStockInitializeResult> InitializeStockAsync(InitializeStockRequest request, CancellationToken cancellationToken = default)
        {
            InitializeRequest = request;
            return Task.FromResult(InventoryStockInitializeResult.Success(new InventoryStockDto { Quantity = request.Quantity ?? 0 }));
        }

        public Task<InventorySerialImportResult> BatchImportSerialsAsync(BatchImportSerialImeiRequest request, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            SerialImportRequest = request;
            return Task.FromResult(SerialImportResult);
        }
    }
}
