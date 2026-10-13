using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Inventory;
using Xunit;

namespace TechStore.Client.Tests.Inventory;

public sealed class StockAdjustmentViewModelTests
{
    [Fact]
    public async Task StockAdjustmentViewModel_NotifiesOnlyAfterSuccessfulRepositoryResultAndPreventsDuplicateSubmit()
    {
        var repository = new FakeInventoryRepository(adjustmentScenario: StockAdjustmentPreviewScenario.Delayed);
        var notifier = new RecordingNotifier();
        var viewModel = CreateViewModel(repository, notifier);
        var balance = await repository.GetStockDetailAsync(new StockBalanceKey("preview-product-06", "preview-variant-07"));
        viewModel.Initialize(balance!.Balance, isPreviewData: true);
        viewModel.QuantityText = "2";
        viewModel.Reason = "Kiểm kê";

        var submit = viewModel.SubmitCommand.ExecuteAsync(null);
        Assert.False(viewModel.SubmitCommand.CanExecute(null));
        Assert.Equal(0, notifier.Calls);
        await submit;

        Assert.Equal(1, notifier.Calls);
        Assert.Equal("preview-product-06", notifier.LastKey!.ProductId);
        Assert.True(viewModel.IsSucceeded);
        Assert.True(viewModel.IsClosed);
        Assert.Equal(8, viewModel.Balance!.PhysicalQuantity);
    }

    [Fact]
    public async Task StockAdjustmentViewModel_ConflictAndErrorRetainDraftAndConflictReloadsOnlyBalance()
    {
        var conflictRepository = new FakeInventoryRepository(adjustmentScenario: StockAdjustmentPreviewScenario.Conflict);
        var notifier = new RecordingNotifier();
        var viewModel = CreateViewModel(conflictRepository, notifier);
        var selected = await conflictRepository.GetStockDetailAsync(new StockBalanceKey("preview-product-06", "preview-variant-07"));
        viewModel.Initialize(selected!.Balance, isPreviewData: true);
        viewModel.Direction = StockAdjustmentDirection.Decrease;
        viewModel.QuantityText = "1";
        viewModel.Reason = "Kiểm kê lệch";
        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsConflict);
        Assert.False(viewModel.IsClosed);
        Assert.Equal(0, notifier.Calls);
        Assert.Equal(StockAdjustmentDirection.Decrease, viewModel.Direction);
        Assert.Equal("1", viewModel.QuantityText);
        Assert.Equal("Kiểm kê lệch", viewModel.Reason);
        await viewModel.ReloadBalanceCommand.ExecuteAsync(null);
        Assert.Equal(6, viewModel.Balance!.PhysicalQuantity);
        Assert.Equal(StockAdjustmentDirection.Decrease, viewModel.Direction);
        Assert.Equal("1", viewModel.QuantityText);
        Assert.Equal("Kiểm kê lệch", viewModel.Reason);

        var errorNotifier = new RecordingNotifier();
        var errorVm = CreateViewModel(new FakeInventoryRepository(adjustmentScenario: StockAdjustmentPreviewScenario.Error), errorNotifier);
        errorVm.Initialize(selected.Balance, isPreviewData: true);
        errorVm.QuantityText = "3";
        errorVm.Reason = "Lý do";
        await errorVm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal("3", errorVm.QuantityText);
        Assert.Equal("Lý do", errorVm.Reason);
        Assert.False(errorVm.IsSucceeded);
        Assert.Equal(0, errorNotifier.Calls);
    }

    [Fact]
    public async Task StockAdjustmentViewModel_CancelDoesNotSubmitOrNotify()
    {
        var notifier = new RecordingNotifier();
        var repository = new FakeInventoryRepository();
        var viewModel = CreateViewModel(repository, notifier);
        var selected = await repository.GetStockDetailAsync(new StockBalanceKey("preview-product-06", "preview-variant-07"));
        viewModel.Initialize(selected!.Balance, isPreviewData: true);
        viewModel.CancelCommand.Execute(null);

        Assert.True(viewModel.IsClosed);
        Assert.Equal(0, notifier.Calls);
        Assert.Equal(6, (await repository.GetStockDetailAsync(new StockBalanceKey("preview-product-06", "preview-variant-07")))!.Balance.PhysicalQuantity);
    }

    [Fact]
    public void StockAdjustmentViewModel_ValidatesDraftAndCalculatesEstimateWithoutMutatingBalance()
    {
        var balance = CreateBalance(serial: false, physical: 10, reserved: 3);
        var viewModel = CreateViewModel();
        viewModel.Initialize(balance, isPreviewData: true);

        Assert.True(viewModel.IsPreviewData);
        Assert.True(viewModel.HasValidationError);
        Assert.False(viewModel.CanSubmit);
        viewModel.QuantityText = "2";
        Assert.True(viewModel.HasValidationError);
        viewModel.Reason = "Kiểm kê";
        Assert.Null(viewModel.ValidationMessage);
        Assert.True(viewModel.CanSubmit);
        Assert.Equal(12, viewModel.ProjectedPhysicalQuantity);
        viewModel.Direction = StockAdjustmentDirection.Decrease;
        Assert.Equal(8, viewModel.ProjectedPhysicalQuantity);
        Assert.Equal(10, balance.PhysicalQuantity);
        Assert.Equal(3, balance.ReservedQuantity);
    }

    [Fact]
    public void StockAdjustmentViewModel_FailsClosedWhenTrackingFlagIsUnknown()
    {
        var unknown = new StockBalance("p", "Phone", "P-1", null, null, null,
            "phones", "Điện thoại", 1m, 2, 0, 1, isSerialTracked: null);
        var viewModel = CreateViewModel();
        viewModel.Initialize(unknown, isPreviewData: true);

        Assert.True(viewModel.IsSerialBlocked);
        Assert.False(viewModel.IsRegularBalance);
        Assert.False(viewModel.CanSubmit);
        Assert.Null(viewModel.ProjectedPhysicalQuantity);
    }

    [Fact]
    public async Task StockAdjustmentViewModel_SerialBranchNavigatesWithExactProductAndVariantContext()
    {
        var navigation = new RecordingInventoryNavigationService();
        var viewModel = new StockAdjustmentViewModel(new FakeInventoryRepository(), new InventoryChangeNotifier(), navigation);
        var serial = new StockBalance("serial-product", "Phone", "P-1", "serial-variant", "256GB", "P-1-V",
            "phones", "Điện thoại", 1m, 3, 1, 1, isSerialTracked: true);
        viewModel.Initialize(serial, isPreviewData: true);

        Assert.False(viewModel.CanSubmit);
        Assert.True(viewModel.NavigateToSerialImeiCommand.CanExecute(null));
        await viewModel.NavigateToSerialImeiCommand.ExecuteAsync(null);
        Assert.Equal("serial-product", navigation.Key!.ProductId);
        Assert.Equal("serial-variant", navigation.Key.VariantId);
    }

    private static StockAdjustmentViewModel CreateViewModel(
        IInventoryRepository? repository = null,
        IInventoryChangeNotifier? notifier = null) => new(
        repository ?? new FakeInventoryRepository(), notifier ?? new InventoryChangeNotifier(), new NoopInventoryNavigationService());

    private static StockBalance CreateBalance(bool serial, int physical, int reserved) => new(
        "p", "Laptop", "L-1", "v", "16GB", "L-1-V", "laptops", "Laptop", 1m,
        physical, reserved, 1, serial);

    private sealed class NoopInventoryNavigationService : IInventoryNavigationService
    {
        public Task<bool> NavigateToSerialImeiAsync(StockBalanceKey key) => Task.FromResult(true);
    }

    private sealed class RecordingInventoryNavigationService : IInventoryNavigationService
    {
        public StockBalanceKey? Key { get; private set; }
        public Task<bool> NavigateToSerialImeiAsync(StockBalanceKey key)
        {
            Key = key;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingNotifier : IInventoryChangeNotifier
    {
        public event System.EventHandler<StockBalanceKey>? StockChanged;
        public int Calls { get; private set; }
        public StockBalanceKey? LastKey { get; private set; }
        public void NotifyStockChanged(StockBalanceKey key)
        {
            Calls++;
            LastKey = key;
            StockChanged?.Invoke(this, key);
        }
    }
}
