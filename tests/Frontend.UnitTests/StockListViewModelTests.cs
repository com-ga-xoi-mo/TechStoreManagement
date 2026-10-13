using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Services.Dialog;
using TechStore.Client.ViewModels.Inventory;
using Xunit;

namespace TechStore.Client.Tests.Inventory;

public sealed class StockListViewModelTests
{
    [Fact]
    public async Task StockListViewModel_OpenDetailsPassesExactProductAndVariantIdentity()
    {
        var dialog = new RecordingStockDetailDialogService();
        var viewModel = new StockListViewModel(
            new FakeInventoryRepository(), new RecordingCategoryRepository(), stockDetailDialogService: dialog);
        // Use a variant row so the detail identity includes both keys.
        var balance = new StockBalance("variant-product", "Laptop", "LAPTOP", "variant-16gb", "16 GB", "LAPTOP-16",
            "laptops", "Laptop", 2_000_000m, 5, 1, 2);

        await viewModel.OpenDetailsCommand.ExecuteAsync(balance);

        Assert.Equal(new StockBalanceKey("variant-product", "variant-16gb"), dialog.Key);
    }

    [Fact]
    public async Task StockListViewModel_OpenAdjustmentPassesExactBalanceAndPreviewProvenanceWithoutMutatingRow()
    {
        var dialog = new RecordingStockAdjustmentDialogService();
        var inventory = new FakeInventoryRepository();
        var viewModel = new StockListViewModel(
            inventory, new RecordingCategoryRepository(), stockAdjustmentDialogService: dialog);
        await viewModel.LoadIfNeededAsync();
        var balance = viewModel.Items[0];
        var originalQuantity = balance.PhysicalQuantity;

        await viewModel.OpenAdjustmentCommand.ExecuteAsync(balance);

        Assert.Same(balance, dialog.Balance);
        Assert.True(dialog.IsPreviewData);
        Assert.True(dialog.Balance!.IsSerialTracked);
        Assert.Equal(originalQuantity, balance.PhysicalQuantity);
    }

    [Fact]
    public async Task StockListViewModel_InventoryChangeMarksSnapshotStaleAndReloadsWhenActive()
    {
        var notifier = new InventoryChangeNotifier();
        var inventory = new CountingInventoryRepository();
        var viewModel = new StockListViewModel(inventory, new RecordingCategoryRepository(), notifier);
        await viewModel.LoadIfNeededAsync();
        Assert.True(viewModel.HasLoaded);
        Assert.Equal(1, inventory.StockPageCalls);

        notifier.NotifyStockChanged(new StockBalanceKey("preview-product-01", "preview-variant-01"));
        await viewModel.WaitForCurrentQueryAsync();

        Assert.True(viewModel.HasLoaded);
        Assert.Equal(20, viewModel.TotalItems);
        Assert.Equal(2, inventory.StockPageCalls);
    }

    [Fact]
    public async Task SuccessfulAdjustmentReloadsActiveListButConflictDoesNotNotifyOrReload()
    {
        var notifier = new InventoryChangeNotifier();
        var successRepository = new FakeInventoryRepository();
        var list = new StockListViewModel(successRepository, new RecordingCategoryRepository(), notifier);
        await list.LoadIfNeededAsync();
        var selected = list.Items[6]; // MacBook Air preview fixture is regular stock.
        Assert.False(selected.IsSerialTracked);

        var adjustment = new StockAdjustmentViewModel(successRepository, notifier, new NoopInventoryNavigationService());
        adjustment.Initialize(selected, isPreviewData: true);
        adjustment.QuantityText = "2";
        adjustment.Reason = "Kiểm kê";
        await adjustment.SubmitCommand.ExecuteAsync(null);
        await list.WaitForCurrentQueryAsync();

        Assert.Equal(8, list.Items[6].PhysicalQuantity);

        var conflictNotifier = new CountingNotifier();
        var conflictRepository = new FakeInventoryRepository(adjustmentScenario: StockAdjustmentPreviewScenario.Conflict);
        var conflictList = new StockListViewModel(conflictRepository, new RecordingCategoryRepository(), conflictNotifier);
        await conflictList.LoadIfNeededAsync();
        var callsBeforeConflict = conflictNotifier.Calls;
        var conflictBalance = conflictList.Items[6];
        var conflictVm = new StockAdjustmentViewModel(conflictRepository, conflictNotifier, new NoopInventoryNavigationService());
        conflictVm.Initialize(conflictBalance, isPreviewData: true);
        conflictVm.QuantityText = "1";
        conflictVm.Reason = "Kiểm kê";
        await conflictVm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(callsBeforeConflict, conflictNotifier.Calls);
    }

    [Fact]
    public async Task StockListViewModel_LoadsStockAndReusesCategoryRepositoryOptions()
    {
        var categoryRepository = new RecordingCategoryRepository();
        var viewModel = new StockListViewModel(new FakeInventoryRepository(), categoryRepository);

        Assert.Empty(viewModel.Items);
        Assert.Empty(viewModel.CategoryOptions);

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(1, categoryRepository.Calls);
        Assert.Equal(5, viewModel.CategoryOptions.Count);
        Assert.Equal("Tất cả danh mục", viewModel.CategoryOptions[0].Name);
        Assert.Equal(8, viewModel.Items.Count);
        Assert.True(viewModel.IsPreviewData);
        Assert.True(viewModel.HasLoaded);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task StockListViewModel_CriteriaChangeResetsToFirstPageAndAppliesCombinedFilters()
    {
        var viewModel = new StockListViewModel(new FakeInventoryRepository(), new RecordingCategoryRepository());
        await viewModel.LoadIfNeededAsync();
        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.PageNumber);

        viewModel.SearchText = "iPhone";
        viewModel.SelectedCategoryId = "phones";
        viewModel.SelectedStatus = InventoryStockStatus.LowStock;
        await viewModel.WaitForCurrentQueryAsync();

        Assert.Equal(1, viewModel.PageNumber);
        Assert.NotEmpty(viewModel.Items);
        Assert.All(viewModel.Items, item =>
        {
            Assert.Contains("iphone", item.ProductName.ToLowerInvariant());
            Assert.Equal("phones", item.CategoryId);
            Assert.Equal(InventoryStockStatus.LowStock, item.Status);
        });
    }

    [Fact]
    public async Task StockListViewModel_ShowsLoadingUntilRepositoryCompletes()
    {
        var repository = new DeferredInventoryRepository();
        var viewModel = new StockListViewModel(repository, new RecordingCategoryRepository());
        var loading = viewModel.LoadIfNeededAsync();
        await repository.FirstRequestStarted.Task;

        Assert.True(viewModel.IsLoading);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasLoaded);

        repository.CompleteFirstRequest(Page([CreateBalance("loaded", "Laptop") ]));
        await loading;

        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.HasLoaded);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task StockListViewModel_ShowsEmptyStateForSuccessfulEmptyResult()
    {
        var viewModel = new StockListViewModel(
            new FakeInventoryRepository(StockListPreviewScenario.Empty), new RecordingCategoryRepository());

        await viewModel.LoadIfNeededAsync();

        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task StockListViewModel_ReportsUnavailableOutsidePreviewWithoutShowingLiveRows()
    {
        var viewModel = new StockListViewModel(
            new UnavailableInventoryRepository(), new RecordingCategoryRepository());

        await viewModel.LoadIfNeededAsync();

        Assert.True(viewModel.HasError);
        Assert.Contains("chưa được cấu hình", viewModel.ErrorMessage);
        Assert.Contains("Không có dữ liệu thật", viewModel.ErrorMessage);
        Assert.False(viewModel.IsPreviewData);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task StockListViewModel_ShowsErrorThenRetryReplacesItWithResult()
    {
        var repository = new FailOnceInventoryRepository();
        var viewModel = new StockListViewModel(repository, new RecordingCategoryRepository());

        await viewModel.LoadIfNeededAsync();

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsEmpty);
        Assert.Empty(viewModel.Items);

        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        Assert.True(viewModel.HasLoaded);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task StockListViewModel_ObsoleteDelayedRequestCannotReplaceLatestResult()
    {
        var repository = new DelayedFirstInventoryRepository();
        var viewModel = new StockListViewModel(repository, new RecordingCategoryRepository());
        var firstLoad = viewModel.LoadIfNeededAsync();
        await repository.FirstRequestStarted.Task;

        viewModel.SearchText = "latest";
        await viewModel.WaitForCurrentQueryAsync();
        Assert.Single(viewModel.Items);
        Assert.Equal("latest", viewModel.Items[0].ProductName);

        repository.CompleteFirstRequest(Page([CreateBalance("old", "obsolete") ]));
        await firstLoad;

        Assert.Single(viewModel.Items);
        Assert.Equal("latest", viewModel.Items[0].ProductName);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task StockListViewModel_CancelPendingLoadDoesNotTurnCancellationIntoError()
    {
        var repository = new DeferredInventoryRepository();
        var viewModel = new StockListViewModel(repository, new RecordingCategoryRepository());
        var loading = viewModel.LoadIfNeededAsync();
        await repository.FirstRequestStarted.Task;

        viewModel.CancelPendingLoad();
        repository.CompleteFirstRequest(Page([CreateBalance("cancelled", "Cancelled") ]));
        await loading;

        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.Items);
    }

    private static StockBalance CreateBalance(string id, string name) => new(
        id, name, $"SKU-{id}", null, null, null, "laptops", "Laptop", 1_000_000m, 4, 1, 1);

    private static StockListPage Page(IReadOnlyList<StockBalance> items) =>
        new(items, items.Count, 1, StockListViewModel.RowsPerPage, IsPreviewData: true);

    private sealed class DeferredInventoryRepository : IInventoryRepository
    {
        private readonly TaskCompletionSource<StockListPage> _firstResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default)
        {
            FirstRequestStarted.TrySetResult(true);
            return await _firstResult.Task;
        }

        public Task<StockDetailSnapshot?> GetStockDetailAsync(StockBalanceKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult<StockDetailSnapshot?>(null);

        public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StockAdjustmentResult.Failure());

        public void CompleteFirstRequest(StockListPage page) => _firstResult.TrySetResult(page);
    }

    private sealed class FailOnceInventoryRepository : IInventoryRepository
    {
        private int _calls;

        public Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_calls++ == 0)
            {
                throw new InvalidOperationException("preview failure");
            }

            return Task.FromResult(Page([CreateBalance("retry", "Recovered") ]));
        }

        public Task<StockDetailSnapshot?> GetStockDetailAsync(StockBalanceKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult<StockDetailSnapshot?>(null);

        public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StockAdjustmentResult.Failure());
    }

    private sealed class DelayedFirstInventoryRepository : IInventoryRepository
    {
        private readonly TaskCompletionSource<StockListPage> _firstResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query.SearchText))
            {
                FirstRequestStarted.TrySetResult(true);
                return _firstResult.Task;
            }

            return Task.FromResult(Page([CreateBalance("latest", query.SearchText)]));
        }

        public Task<StockDetailSnapshot?> GetStockDetailAsync(StockBalanceKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult<StockDetailSnapshot?>(null);

        public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StockAdjustmentResult.Failure());

        public void CompleteFirstRequest(StockListPage page) => _firstResult.TrySetResult(page);
    }

    private sealed class RecordingCategoryRepository : ICategoryRepository
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            IReadOnlyList<CategoryOption> result =
            [
                new("phones", "Điện thoại"),
                new("laptops", "Laptop"),
                new("tablets", "Tablet"),
                new("accessories", "Phụ kiện")
            ];
            return Task.FromResult(result);
        }

        public Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CategoryTreeSnapshot([], IsPreviewData: true));
        }
    }

    private sealed class CountingInventoryRepository : IInventoryRepository
    {
        private readonly FakeInventoryRepository _inner = new();
        public int StockPageCalls { get; private set; }

        public Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default)
        {
            StockPageCalls++;
            return _inner.GetStockPageAsync(query, cancellationToken);
        }

        public Task<StockDetailSnapshot?> GetStockDetailAsync(StockBalanceKey key, CancellationToken cancellationToken = default) =>
            _inner.GetStockDetailAsync(key, cancellationToken);

        public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            _inner.AdjustStockAsync(request, cancellationToken);
    }

    private sealed class RecordingStockDetailDialogService : IStockDetailDialogService
    {
        public StockBalanceKey? Key { get; private set; }
        public Task ShowAsync(StockBalanceKey key)
        {
            Key = key;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingNotifier : IInventoryChangeNotifier
    {
        public event EventHandler<StockBalanceKey>? StockChanged;
        public int Calls { get; private set; }
        public void NotifyStockChanged(StockBalanceKey key)
        {
            Calls++;
            StockChanged?.Invoke(this, key);
        }
    }

    private sealed class NoopInventoryNavigationService : TechStore.Client.Services.Navigation.IInventoryNavigationService
    {
        public Task<bool> NavigateToSerialImeiAsync(StockBalanceKey key) => Task.FromResult(true);
    }

    private sealed class RecordingStockAdjustmentDialogService : IStockAdjustmentDialogService
    {
        public StockBalance? Balance { get; private set; }
        public bool IsPreviewData { get; private set; }
        public Task ShowAsync(StockBalance balance, bool isPreviewData)
        {
            Balance = balance;
            IsPreviewData = isPreviewData;
            return Task.CompletedTask;
        }
    }
}
