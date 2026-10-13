using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Inventory;
using Xunit;

namespace TechStore.Client.Tests.Inventory;

public sealed class InventoryViewModelTests
{
    [Fact]
    public async Task InventoryViewModel_ConsumesSerialImeiContextAndKeepsFeatureExplicitlyUnimplemented()
    {
        var repository = new RecordingCategoryRepository();
        var context = new TestInventoryNavigationContext();
        var viewModel = new InventoryViewModel(
            new CategoryListViewModel(repository),
            new StockListViewModel(new FakeInventoryRepository(), repository),
            context);
        var key = new StockBalanceKey("product-9", "variant-2");
        context.RequestSerialImei(key);

        await viewModel.InitializeAsync();

        Assert.Equal(InventoryTab.SerialImei, viewModel.SelectedTab);
        Assert.Equal(key, viewModel.SelectedSerialImeiContext);
        Assert.Contains("chưa được triển khai", viewModel.PlaceholderTitle);
        Assert.Contains("Không có dữ liệu mã máy mẫu", viewModel.PlaceholderMessage);
        Assert.Null(context.PendingSerialImeiKey);
    }

    [Fact]
    public async Task InventoryNavigationService_StoresExactContextAndNavigatesToInventory()
    {
        var context = new InventoryNavigationContext();
        var navigation = new RecordingNavigationService();
        var service = new InventoryNavigationService(navigation, context);
        var key = new StockBalanceKey("product-9", "variant-2");

        var navigated = await service.NavigateToSerialImeiAsync(key);

        Assert.True(navigated);
        Assert.Equal(RouteId.Inventory, navigation.LastRoute);
        Assert.Equal(key, context.PendingSerialImeiKey);
    }

    [Fact]
    public async Task InventoryViewModel_DefaultsToCategoryAndLoadsOnlyWhenInitialized()
    {
        var repository = new RecordingCategoryRepository();
        var categoryViewModel = new CategoryListViewModel(repository);
        var stockViewModel = new StockListViewModel(new FakeInventoryRepository(), repository);
        var viewModel = new InventoryViewModel(categoryViewModel, stockViewModel);

        Assert.Equal(InventoryTab.Categories, viewModel.SelectedTab);
        Assert.Equal(0, repository.TreeCalls);
        Assert.False(categoryViewModel.HasLoaded);

        await viewModel.InitializeAsync();

        Assert.Equal(1, repository.TreeCalls);
        Assert.True(categoryViewModel.HasLoaded);
    }

    [Fact]
    public async Task InventoryViewModel_UnfinishedTabsDoNotLoadCategoryTreeAndReturningPreservesSnapshot()
    {
        var repository = new RecordingCategoryRepository();
        var categoryViewModel = new CategoryListViewModel(repository);
        var stockViewModel = new StockListViewModel(new FakeInventoryRepository(), repository);
        var viewModel = new InventoryViewModel(categoryViewModel, stockViewModel);

        await viewModel.SelectStockTabCommand.ExecuteAsync(null);
        Assert.Equal(InventoryTab.Stock, viewModel.SelectedTab);
        Assert.True(stockViewModel.HasLoaded);
        Assert.Equal(8, stockViewModel.Items.Count);
        Assert.Equal(0, repository.TreeCalls);

        viewModel.SelectSerialImeiTabCommand.Execute(null);
        Assert.Equal(InventoryTab.SerialImei, viewModel.SelectedTab);
        Assert.Contains("UI-12", viewModel.PlaceholderMessage);
        Assert.Equal(0, repository.TreeCalls);

        await viewModel.SelectCategoryTabCommand.ExecuteAsync(null);
        await viewModel.SelectStockTabCommand.ExecuteAsync(null);
        await viewModel.SelectCategoryTabCommand.ExecuteAsync(null);

        Assert.Equal(1, repository.TreeCalls);
        Assert.True(categoryViewModel.HasLoaded);
    }

    private sealed class RecordingCategoryRepository : ICategoryRepository
    {
        public int TreeCalls { get; private set; }

        public Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<CategoryOption>>([]);
        }

        public Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TreeCalls++;
            return Task.FromResult(new CategoryTreeSnapshot([], IsPreviewData: true));
        }
    }

    private sealed class TestInventoryNavigationContext : IInventoryNavigationContext
    {
        public StockBalanceKey? PendingSerialImeiKey { get; private set; }
        public event System.EventHandler<StockBalanceKey>? SerialImeiRequested;

        public void RequestSerialImei(StockBalanceKey key)
        {
            PendingSerialImeiKey = key;
            SerialImeiRequested?.Invoke(this, key);
        }

        public StockBalanceKey? ConsumePendingSerialImeiKey()
        {
            var key = PendingSerialImeiKey;
            PendingSerialImeiKey = null;
            return key;
        }
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public RouteId LastRoute { get; private set; }
        public RouteId CurrentRoute => LastRoute;
        public event System.EventHandler<RouteId>? RouteChanged;
        public System.Collections.Generic.IReadOnlyList<RouteMetadata> GetAllowedMenuRoutes() => [];
        public bool NavigateTo(RouteId routeId)
        {
            LastRoute = routeId;
            RouteChanged?.Invoke(this, routeId);
            return true;
        }
        public bool GoBack() => false;
        public void Reset() { }
        public void SetFrameAdapter(IFrameAdapter frameAdapter) { }
    }
}
