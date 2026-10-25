using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Features.Inventory.ViewModels;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Features.Inventory;

public sealed class DeleteStockItemDialogViewModelTests
{
    private static readonly InventoryStockDeleteRequest Target = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "iPhone 15 Pro Max",
        "IP15PM-256",
        12);

    [Fact]
    public void Constructor_PreservesTargetSnapshotAndSerialWarning()
    {
        var vm = new DeleteStockItemDialogViewModel(new FakeApi(), Target);

        Assert.Equal(Target.StockId, vm.StockId);
        Assert.Equal(Target.ProductId, vm.ProductId);
        Assert.Equal("iPhone 15 Pro Max", vm.ProductName);
        Assert.Equal("IP15PM-256", vm.Sku);
        Assert.Equal(12, vm.TotalSerialsCount);
        Assert.Contains("12 mã Serial / IMEI", vm.SerialWarningText);
    }

    [Fact]
    public async Task Cancel_ClosesWithoutCallingDelete()
    {
        var api = new FakeApi();
        var vm = new DeleteStockItemDialogViewModel(api, Target);
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;

        vm.CancelCommand.Execute(null);
        await Task.CompletedTask;

        Assert.Equal(1, closed);
        Assert.Equal(0, api.DeleteCalls);
    }

    [Fact]
    public async Task UnsupportedEndpoint_KeepsDialogOpenAndPreservesContext()
    {
        var api = new FakeApi { DeleteResult = InventoryStockDeleteResult.Unsupported("Endpoint chưa sẵn sàng.") };
        var vm = new DeleteStockItemDialogViewModel(api, Target);
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, api.DeleteCalls);
        Assert.Equal(0, closed);
        Assert.Equal("Endpoint chưa sẵn sàng.", vm.ErrorMessage);
        Assert.Equal(Target.StockId, vm.StockId);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Success_ClosesOnceAndPublishesRefreshPayload()
    {
        var api = new FakeApi { DeleteResult = InventoryStockDeleteResult.Success() };
        var vm = new DeleteStockItemDialogViewModel(api, Target);
        InventoryStockDeleteCompleted? completed = null;
        var closed = 0;
        vm.DeleteSucceeded += (_, value) => completed = value;
        vm.RequestClose += (_, _) => closed++;

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, api.DeleteCalls);
        Assert.Equal(1, closed);
        Assert.NotNull(completed);
        Assert.Equal(Target.StockId, completed!.StockId);
        Assert.Contains("Đã xóa sản phẩm", completed.Message);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task RepeatedConfirmWhileBusy_SendsOnlyOneRequest()
    {
        var completion = new TaskCompletionSource<InventoryStockDeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeApi { DeleteHandler = (_, _) => completion.Task };
        var vm = new DeleteStockItemDialogViewModel(api, Target);

        var first = vm.ConfirmDeleteCommand.ExecuteAsync(null);
        var second = vm.ConfirmDeleteCommand.ExecuteAsync(null);
        completion.SetResult(InventoryStockDeleteResult.Unsupported());
        await Task.WhenAll(first, second);

        Assert.Equal(1, api.DeleteCalls);
    }

    [Theory]
    [InlineData(401, "Phiên đăng nhập đã hết hạn")]
    [InlineData(403, "không có quyền")]
    [InlineData(400, "dữ liệu sản phẩm")]
    [InlineData(404, "không còn tồn tại")]
    [InlineData(409, "dữ liệu liên kết")]
    public async Task HttpFailure_KeepsDialogOpenWithFriendlyError(int status, string expected)
    {
        var api = new FakeApi { DeleteException = ApiException.Http(status, null) };
        var vm = new DeleteStockItemDialogViewModel(api, Target);
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Equal(0, closed);
        Assert.Contains(expected, vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    private sealed class FakeApi : IInventoryRepository
    {
        public int DeleteCalls { get; private set; }
        public InventoryStockDeleteResult DeleteResult { get; init; } = InventoryStockDeleteResult.Unsupported();
        public Exception? DeleteException { get; init; }
        public Func<Guid, CancellationToken, Task<InventoryStockDeleteResult>>? DeleteHandler { get; init; }

        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResultDto<InventoryStockDto>());

        public Task<InventoryStockDeleteResult> DeleteStockAsync(Guid stockId, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            if (DeleteException is not null) return Task.FromException<InventoryStockDeleteResult>(DeleteException);
            if (DeleteHandler is not null) return DeleteHandler(stockId, cancellationToken);
            return Task.FromResult(DeleteResult);
        }
    }
}
