using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Models.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class StockAdjustmentDialogViewModelTests
{
    [Fact]
    public void Projection_ChangesWithIncreaseAndDecrease()
    {
        var vm = new StockAdjustmentDialogViewModel(new InventoryStockRow(Stock(10, 2)), new FakeApi());
        vm.Amount = 3;
        Assert.Equal(13, vm.NewQuantity);
        vm.SetDecreaseCommand.Execute(null);
        Assert.Equal(7, vm.NewQuantity);
        Assert.Equal("Tồn mới: 10 → 7", vm.PreviewText);
    }

    [Fact]
    public async Task Confirm_RequiresReasonAndPreventsReservedViolation()
    {
        var api = new FakeApi();
        var vm = new StockAdjustmentDialogViewModel(new InventoryStockRow(Stock(10, 8)), api)
        {
            Amount = 3,
            Kind = StockAdjustmentKind.Decrease
        };
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(0, api.Calls);
        Assert.False(vm.CanSubmit);

        vm.Reason = "Kiểm kê thực tế";
        Assert.False(vm.CanSubmit);
        vm.Amount = 2;
        Assert.True(vm.CanSubmit);
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(-2, api.LastRequest!.QuantityChange);
    }

    [Fact]
    public async Task Failure_KeepsDialogOpenAndClearsBusy()
    {
        var vm = new StockAdjustmentDialogViewModel(new InventoryStockRow(Stock(10, 0)), new FakeApi
        {
            Result = new InventoryStockAdjustmentResult(false, Failure: InventoryStockDetailFailure.Conflict, Message: "Conflict")
        })
        {
            Amount = 1,
            Reason = "Kiểm kê"
        };
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("Conflict", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.Equal(0, closed);
    }

    private static InventoryStockDto Stock(int quantity, int reserved) => new()
    {
        Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "iPhone", ProductSku = "IP-1",
        Quantity = quantity, ReservedQuantity = reserved
    };

    private sealed class FakeApi : IInventoryRepository
    {
        public int Calls { get; private set; }
        public AdjustStockRequest? LastRequest { get; private set; }
        public InventoryStockAdjustmentResult Result { get; init; } = InventoryStockAdjustmentResult.Success(new InventoryStockDetailDto());
        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResultDto<InventoryStockDto>());
        public Task<InventoryStockAdjustmentResult> AdjustStockAsync(Guid stockId, AdjustStockRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
