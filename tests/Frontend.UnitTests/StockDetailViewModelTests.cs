using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Inventory;
using Xunit;

namespace TechStore.Client.Tests.Inventory;

public sealed class StockDetailViewModelTests
{
    private static readonly StockBalanceKey IPhoneKey = new("preview-product-01", "preview-variant-01");

    [Fact]
    public async Task StockDetailViewModel_SerialImeiCommandRoutesSelectedProductAndVariant()
    {
        var navigation = new RecordingInventoryNavigationService();
        using var viewModel = new StockDetailViewModel(new FakeInventoryRepository(), inventoryNavigationService: navigation);
        await viewModel.LoadAsync(IPhoneKey);

        await viewModel.NavigateToSerialImeiCommand.ExecuteAsync(null);

        Assert.Equal(IPhoneKey, navigation.Key);
        Assert.Null(viewModel.NavigationErrorMessage);
    }

    [Fact]
    public async Task StockDetailViewModel_LoadsBalanceAndMovementsAsOnePreviewSnapshot()
    {
        var viewModel = new StockDetailViewModel(new FakeInventoryRepository());

        await viewModel.LoadAsync(IPhoneKey);

        Assert.True(viewModel.HasLoaded);
        Assert.True(viewModel.HasBalance);
        Assert.True(viewModel.IsPreviewData);
        Assert.Equal(IPhoneKey, viewModel.Key);
        Assert.Equal(8, viewModel.Balance!.PhysicalQuantity);
        Assert.Equal(5, viewModel.RecentMovements.Count);
        Assert.NotNull(viewModel.LastRefreshedAt);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task StockDetailViewModel_DistinguishesEmptyHistoryAndNotFound()
    {
        var emptyHistory = new StockDetailViewModel(
            new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.EmptyHistory));
        await emptyHistory.LoadAsync(IPhoneKey);

        Assert.True(emptyHistory.HasBalance);
        Assert.True(emptyHistory.IsEmptyHistory);
        Assert.False(emptyHistory.IsNotFound);

        var notFound = new StockDetailViewModel(
            new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.NotFound));
        await notFound.LoadAsync(IPhoneKey);

        Assert.True(notFound.IsNotFound);
        Assert.False(notFound.IsEmptyHistory);
        Assert.Null(notFound.Balance);
    }

    [Fact]
    public async Task StockDetailViewModel_ShowsUnavailableErrorWithoutPreviewFallback()
    {
        var viewModel = new StockDetailViewModel(new UnavailableInventoryRepository());

        await viewModel.LoadAsync(IPhoneKey);

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsPreviewData);
        Assert.Null(viewModel.Balance);
        Assert.Contains("Không có dữ liệu thật", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task StockDetailViewModel_RetryUsesCurrentBalanceAfterFailure()
    {
        var viewModel = new StockDetailViewModel(
            new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.FailOnce));
        await viewModel.LoadAsync(IPhoneKey);

        Assert.True(viewModel.HasError);
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasBalance);
        Assert.False(viewModel.HasError);
        Assert.Equal(IPhoneKey, viewModel.Key);
    }

    [Fact]
    public async Task StockDetailViewModel_CancelLoadLeavesNoFalseSuccessOrError()
    {
        var viewModel = new StockDetailViewModel(
            new FakeInventoryRepository(detailScenario: StockDetailPreviewScenario.Delayed));
        var load = viewModel.LoadAsync(IPhoneKey);
        viewModel.CancelLoad();

        await load;

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasLoaded);
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.Balance);
    }

    [Fact]
    public async Task StockDetailViewModel_RefreshReloadsAndReplacesTheWholeSnapshotForSameKey()
    {
        var first = Snapshot(8, "movement-first", DateTimeOffset.UtcNow.AddMinutes(-2));
        var second = Snapshot(6, "movement-second", DateTimeOffset.UtcNow);
        var repository = new SequenceDetailRepository(first, second);
        var viewModel = new StockDetailViewModel(repository);
        await viewModel.LoadAsync(IPhoneKey);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, repository.DetailCalls);
        Assert.Equal(IPhoneKey, repository.RequestedKeys[0]);
        Assert.Equal(IPhoneKey, repository.RequestedKeys[1]);
        Assert.Equal(6, viewModel.Balance!.PhysicalQuantity);
        Assert.Equal("movement-second", Assert.Single(viewModel.RecentMovements).MovementId);
        Assert.Equal(second.LastRefreshedAt, viewModel.LastRefreshedAt);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task StockDetailViewModel_FailedRefreshDoesNotKeepAStaleSuccessSnapshot()
    {
        var repository = new SequenceDetailRepository(Snapshot(8, "movement-first", DateTimeOffset.UtcNow), null);
        repository.FailOnSecondDetailCall = true;
        var viewModel = new StockDetailViewModel(repository);
        await viewModel.LoadAsync(IPhoneKey);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.HasLoaded);
        Assert.False(viewModel.HasBalance);
        Assert.Null(viewModel.Balance);
        Assert.Empty(viewModel.RecentMovements);
    }

    [Fact]
    public async Task StockDetailViewModel_OnlyMatchingInventoryChangeRefreshesSelectedBalance()
    {
        var notifier = new InventoryChangeNotifier();
        var repository = new SequenceDetailRepository(
            Snapshot(8, "movement-first", DateTimeOffset.UtcNow),
            Snapshot(7, "movement-next", DateTimeOffset.UtcNow));
        using var viewModel = new StockDetailViewModel(repository, notifier);
        await viewModel.LoadAsync(IPhoneKey);

        notifier.NotifyStockChanged(new StockBalanceKey("another-product", "variant"));
        await Task.Yield();
        Assert.Equal(1, repository.DetailCalls);

        notifier.NotifyStockChanged(IPhoneKey);
        await viewModel.WaitForCurrentQueryAsync();

        Assert.Equal(2, repository.DetailCalls);
        Assert.Equal(7, viewModel.Balance!.PhysicalQuantity);
    }

    [Fact]
    public async Task StockDetailViewModel_LatestRequestWinsWhenOlderRequestCompletesLast()
    {
        var repository = new OverlappingDetailRepository();
        var viewModel = new StockDetailViewModel(repository);
        var older = viewModel.LoadAsync(new StockBalanceKey("older-product", "older-variant"));
        await repository.OlderRequestStarted.Task;

        await viewModel.LoadAsync(new StockBalanceKey("newer-product", "newer-variant"));
        repository.CompleteOlder(Snapshot("older-product", "older-variant", 3, "older-event"));
        await older;

        Assert.Equal("newer-product", viewModel.Balance!.ProductId);
        Assert.Equal("newer-variant", viewModel.Balance.VariantId);
        Assert.Equal("newer-event", Assert.Single(viewModel.RecentMovements).MovementId);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasError);
    }

    private static StockDetailSnapshot Snapshot(int physical, string movementId, DateTimeOffset refreshedAt) =>
        Snapshot("preview-product-01", "preview-variant-01", physical, movementId, refreshedAt);

    private static StockDetailSnapshot Snapshot(
        string productId,
        string variantId,
        int physical,
        string movementId,
        DateTimeOffset? refreshedAt = null)
    {
        var timestamp = refreshedAt ?? DateTimeOffset.UtcNow;
        var balance = new StockBalance(productId, "iPhone", "APL-IP15PM", variantId,
            "256GB", "APL-IP15PM-01", "phones", "Điện thoại", 34_990_000m, physical, 2, 3);
        var movement = new StockMovement(movementId, timestamp, StockMovementType.Adjustment, -1, physical, "Preview");
        return new StockDetailSnapshot(balance, [movement], timestamp, isPreviewData: true);
    }

    private sealed class SequenceDetailRepository(params StockDetailSnapshot?[] details) : IInventoryRepository
    {
        private int _detailCalls;
        public List<StockBalanceKey> RequestedKeys { get; } = [];
        public int DetailCalls => _detailCalls;
        public bool FailOnSecondDetailCall { get; set; }

        public Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StockListPage([], 0, 1, query.PageSize, IsPreviewData: true));

        public Task<StockDetailSnapshot?> GetStockDetailAsync(StockBalanceKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedKeys.Add(key);
            _detailCalls++;
            if (FailOnSecondDetailCall && _detailCalls == 2)
            {
                throw new InvalidOperationException("Refresh failed");
            }

            var index = Math.Min(_detailCalls - 1, details.Length - 1);
            return Task.FromResult(details[index]);
        }

        public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StockAdjustmentResult.Failure());
    }

    private sealed class OverlappingDetailRepository : IInventoryRepository
    {
        private readonly TaskCompletionSource<StockDetailSnapshot?> _olderResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> OlderRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StockListPage([], 0, 1, query.PageSize, IsPreviewData: true));

        public Task<StockDetailSnapshot?> GetStockDetailAsync(StockBalanceKey key, CancellationToken cancellationToken = default)
        {
            if (key.ProductId == "older-product")
            {
                OlderRequestStarted.TrySetResult(true);
                return _olderResult.Task;
            }

            return Task.FromResult<StockDetailSnapshot?>(Snapshot(
                key.ProductId, key.VariantId!, 4, "newer-event"));
        }

        public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StockAdjustmentResult.Failure());

        public void CompleteOlder(StockDetailSnapshot snapshot) => _olderResult.TrySetResult(snapshot);
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
}
