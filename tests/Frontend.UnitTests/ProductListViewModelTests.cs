using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Dialog;
using TechStore.Client.ViewModels.Catalog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TechStore.Client.Models.Identity;
using TechStore.Client.Services.Session;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductListViewModelTests
{
    [Fact]
    public async Task OpenProductEdit_OnSaveRefreshesUsingCurrentCriteriaAndPage()
    {
        var repository = new StubProductRepository(criteria => Task.FromResult(Page(16, criteria.PageIndex, 8)));
        var editDialog = new RecordingProductEditDialogService(new ProductEditDialogResult(ProductEditDialogOutcome.Saved));
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview editor", new HashSet<string> { ProductEditViewModel.EditCapability }));
        var viewModel = new ProductListViewModel(repository, new RecordingProductDetailDialogService(),
            new RecordingProductCreateDialogService(new ProductCreateDialogResult(ProductCreateDialogOutcome.Cancelled)), editDialog, session);
        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "MacBook";
        viewModel.SelectedCategoryId = "laptops";
        viewModel.SelectedBrand = "Apple";
        await Task.Delay(400);
        await viewModel.SearchAsync();
        await viewModel.NextPageAsync();
        var priorCriteria = repository.Criteria.Last();
        var priorCalls = repository.Criteria.Count;

        await viewModel.OpenProductEditCommand.ExecuteAsync("product-1");

        Assert.Equal("product-1", editDialog.ProductId);
        Assert.Equal(priorCalls + 1, repository.Criteria.Count);
        Assert.Equal(priorCriteria, repository.Criteria.Last());
        Assert.Equal(2, viewModel.CurrentPage);
    }

    [Theory]
    [InlineData(ProductEditDialogOutcome.Cancelled)]
    [InlineData(ProductEditDialogOutcome.Failed)]
    public async Task OpenProductEdit_CancelOrFailureLeavesListUntouched(ProductEditDialogOutcome outcome)
    {
        var repository = new StubProductRepository(_ => Task.FromResult(Page(1, 0, 1)));
        var editDialog = new RecordingProductEditDialogService(new ProductEditDialogResult(outcome));
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview editor", new HashSet<string> { ProductEditViewModel.EditCapability }));
        var viewModel = new ProductListViewModel(repository, new RecordingProductDetailDialogService(),
            new RecordingProductCreateDialogService(new ProductCreateDialogResult(ProductCreateDialogOutcome.Cancelled)), editDialog, session);
        await viewModel.InitializeAsync();
        var card = Assert.Single(viewModel.Products);
        var previousCalls = repository.Criteria.Count;

        await viewModel.OpenProductEditCommand.ExecuteAsync(card.Id);

        Assert.Same(card, Assert.Single(viewModel.Products));
        Assert.Equal(previousCalls, repository.Criteria.Count);
    }

    [Fact]
    public async Task OpenProductEdit_IsHiddenAndBlockedWithoutCapability()
    {
        var repository = new StubProductRepository(_ => Task.FromResult(Page(1, 0, 1)));
        var editDialog = new RecordingProductEditDialogService(new ProductEditDialogResult(ProductEditDialogOutcome.Saved));
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview reader", new HashSet<string>()));
        var viewModel = new ProductListViewModel(repository, new RecordingProductDetailDialogService(),
            new RecordingProductCreateDialogService(new ProductCreateDialogResult(ProductCreateDialogOutcome.Cancelled)), editDialog, session);
        await viewModel.InitializeAsync();
        var card = Assert.Single(viewModel.Products);

        await viewModel.OpenProductEditCommand.ExecuteAsync(card.Id);

        Assert.False(viewModel.CanEditProducts);
        Assert.False(card.IsEditActionVisible);
        Assert.Null(editDialog.ProductId);
    }

    [Fact]
    public async Task OpenProductEdit_SavesIntoSharedFakeAndRefreshesVisibleCard()
    {
        var repository = new FakeProductRepository();
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview editor", new HashSet<string> { ProductEditViewModel.EditCapability }));
        var editDialog = new SavingProductEditDialogService(repository);
        var viewModel = new ProductListViewModel(repository, new RecordingProductDetailDialogService(),
            new RecordingProductCreateDialogService(new ProductCreateDialogResult(ProductCreateDialogOutcome.Cancelled)), editDialog, session);
        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "iPhone";
        viewModel.SelectedBrand = "Apple";
        viewModel.SelectedCategoryId = "phones";
        await Task.Delay(400);
        var card = Assert.Single(viewModel.Products);

        await viewModel.OpenProductEditCommand.ExecuteAsync(card.Id);

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal("Updated iPhone 15 Pro Max", Assert.Single(viewModel.Products).Name);
        Assert.Equal("iPhone", viewModel.SearchQuery);
        Assert.Equal("Apple", viewModel.SelectedBrand);
        Assert.Equal("phones", viewModel.SelectedCategoryId);
    }

    [Fact]
    public async Task OpenProductDetail_PassesSelectedIdAndPreservesListContext()
    {
        var repository = new StubProductRepository(_ => Task.FromResult(PageWithProduct("phone-1")));
        var dialog = new RecordingProductDetailDialogService();
        var viewModel = new ProductListViewModel(repository, dialog);
        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "phone";
        viewModel.SelectedBrand = "Apple";
        await Task.Delay(400);
        var card = Assert.Single(viewModel.Products);
        var criteriaBeforeOpen = repository.Criteria.Last();
        var pageBeforeOpen = viewModel.CurrentPage;

        await viewModel.OpenProductDetailCommand.ExecuteAsync(card.Id);

        Assert.Equal("phone-1", dialog.ProductId);
        Assert.Same(card, Assert.Single(viewModel.Products));
        Assert.Equal(pageBeforeOpen, viewModel.CurrentPage);
        Assert.Equal(criteriaBeforeOpen, repository.Criteria.Last());
    }

    [Fact]
    public async Task OpenProductCreate_OnSuccessRefreshesUsingCurrentListCriteria()
    {
        var repository = new StubProductRepository(criteria => Task.FromResult(Page(16, criteria.PageIndex, 8)));
        var dialog = new RecordingProductCreateDialogService(new ProductCreateDialogResult(
            ProductCreateDialogOutcome.Created,
            PageWithProduct("created-product").Items[0]));
        var viewModel = new ProductListViewModel(repository, new RecordingProductDetailDialogService(), dialog);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = "iPhone";
        viewModel.SelectedCategoryId = "phones";
        viewModel.SelectedBrand = "Apple";
        viewModel.SelectedPriceBand = ProductPriceBand.Above25Million;
        viewModel.SelectedStockStatus = ProductStatus.InStock;
        viewModel.SelectedSortMode = ProductSortMode.PriceDescending;
        await Task.Delay(400);
        await viewModel.SearchAsync();
        await viewModel.NextPageAsync();
        var criteriaBeforeCreate = repository.Criteria.Last();
        var callCountBeforeCreate = repository.Criteria.Count;

        await viewModel.OpenProductCreateCommand.ExecuteAsync(null);

        Assert.Equal(callCountBeforeCreate + 1, repository.Criteria.Count);
        Assert.Equal(criteriaBeforeCreate, repository.Criteria.Last());
        Assert.True(viewModel.IsCreateSuccess);
        Assert.Contains("preview", viewModel.CreateSuccessMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, viewModel.CurrentPage);
    }

    [Theory]
    [InlineData(ProductCreateDialogOutcome.Cancelled)]
    [InlineData(ProductCreateDialogOutcome.Failed)]
    public async Task OpenProductCreate_CancelOrFailureDoesNotRefreshList(ProductCreateDialogOutcome outcome)
    {
        var repository = new StubProductRepository(_ => Task.FromResult(Page(1, 0, 1)));
        var dialog = new RecordingProductCreateDialogService(new ProductCreateDialogResult(outcome));
        var viewModel = new ProductListViewModel(repository, dialog);
        await viewModel.InitializeAsync();
        var callCountBeforeCreate = repository.Criteria.Count;

        await viewModel.OpenProductCreateCommand.ExecuteAsync(null);

        Assert.Equal(callCountBeforeCreate, repository.Criteria.Count);
        Assert.False(viewModel.IsCreateSuccess);
    }

    [Fact]
    public async Task Initialize_PopulatesCardsAndCountRange()
    {
        var repository = new StubProductRepository(_ => Task.FromResult(Page(10, 1, 2)));
        var viewModel = new ProductListViewModel(repository);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.IsEmpty);
        Assert.True(viewModel.IsProductGridVisible);
        Assert.Equal(2, viewModel.Products.Count);
        Assert.Equal(10, viewModel.TotalItems);
        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(2, viewModel.TotalPages);
        Assert.Equal("Hiển thị 9–10 trong 10 sản phẩm", viewModel.ResultRangeText);
    }

    [Fact]
    public async Task Initialize_ReportsEmptyResultWithoutFakeCards()
    {
        var viewModel = new ProductListViewModel(new StubProductRepository(_ =>
            Task.FromResult(new PagedResult<ProductSummary>(Array.Empty<ProductSummary>(), 0, 0, 8))));

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.IsProductGridVisible);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsError);
        Assert.Empty(viewModel.Products);
        Assert.Equal("Hiển thị 0 sản phẩm", viewModel.ResultRangeText);
    }

    [Fact]
    public async Task Initialize_ReportsLoadingUntilRepositoryCompletes()
    {
        var completion = new TaskCompletionSource<PagedResult<ProductSummary>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new ProductListViewModel(new StubProductRepository(_ => completion.Task));

        var pending = viewModel.InitializeAsync();

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.IsProductGridVisible);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.NextPageCommand.CanExecute(null));
        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
        completion.SetResult(Page(1, 0, 1));
        await pending;

        Assert.False(viewModel.IsLoading);
        Assert.Single(viewModel.Products);
    }

    [Fact]
    public async Task PageCommands_AreEnabledOnlyWhenTheirBoundaryAllowsNavigation()
    {
        var repository = new StubProductRepository(criteria =>
            Task.FromResult(Page(9, criteria.PageIndex, criteria.PageIndex == 0 ? 8 : 1)));
        var viewModel = new ProductListViewModel(repository);

        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
        Assert.False(viewModel.NextPageCommand.CanExecute(null));

        await viewModel.InitializeAsync();
        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
        Assert.True(viewModel.NextPageCommand.CanExecute(null));

        await viewModel.NextPageAsync();
        Assert.True(viewModel.PreviousPageCommand.CanExecute(null));
        Assert.False(viewModel.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public void FilterIndices_MapToTypedCriteriaOptions()
    {
        var repository = new StubProductRepository(_ => Task.FromResult(Page(1, 0, 1)));
        var viewModel = new ProductListViewModel(repository);

        viewModel.SelectedPriceBandIndex = (int)ProductPriceBand.Above25Million;
        viewModel.SelectedStockStatusIndex = (int)ProductStatus.Discontinued + 1;
        viewModel.SelectedSortModeIndex = (int)ProductSortMode.StockDescending;

        var criteria = repository.Criteria.Last();
        Assert.Equal(ProductPriceBand.Above25Million, criteria.PriceBand);
        Assert.Equal(ProductStatus.Discontinued, criteria.StockStatus);
        Assert.Equal(ProductSortMode.StockDescending, criteria.SortMode);
    }

    [Fact]
    public async Task Retry_ClearsErrorAfterSuccessfulRepositoryResponse()
    {
        var calls = 0;
        var repository = new StubProductRepository(_ =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<PagedResult<ProductSummary>>(new InvalidOperationException("offline"))
                : Task.FromResult(Page(1, 0, 1));
        });
        var viewModel = new ProductListViewModel(repository);

        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsError);
        Assert.False(viewModel.IsProductGridVisible);
        Assert.Empty(viewModel.Products);

        await viewModel.RetryAsync();

        Assert.False(viewModel.IsError);
        Assert.True(viewModel.IsProductGridVisible);
        Assert.False(viewModel.IsLoading);
        Assert.Single(viewModel.Products);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Retry_PreservesCurrentPageAndFilterCriteria()
    {
        var repository = new StubProductRepository(criteria =>
            Task.FromResult(Page(16, criteria.PageIndex, 1)));
        var viewModel = new ProductListViewModel(repository)
        {
            SearchQuery = "iPhone",
            SelectedCategoryId = "phones",
            SelectedBrand = "Apple",
            SelectedPriceBand = ProductPriceBand.Above25Million,
            SelectedStockStatus = ProductStatus.InStock,
            SelectedSortMode = ProductSortMode.PriceDescending
        };

        await viewModel.InitializeAsync();
        await viewModel.NextPageAsync();
        await viewModel.RetryAsync();

        var criteria = repository.Criteria.Last();
        Assert.Equal(1, criteria.PageIndex);
        Assert.Equal("iPhone", criteria.SearchQuery);
        Assert.Equal("phones", criteria.CategoryId);
        Assert.Equal("Apple", criteria.Brand);
        Assert.Equal(ProductPriceBand.Above25Million, criteria.PriceBand);
        Assert.Equal(ProductStatus.InStock, criteria.StockStatus);
        Assert.Equal(ProductSortMode.PriceDescending, criteria.SortMode);
    }

    [Fact]
    public async Task SearchQuery_IsDebouncedBeforeCombinedCriteriaIsRequested()
    {
        var repository = new StubProductRepository(_ => Task.FromResult(Page(0, 0, 0)));
        var viewModel = new ProductListViewModel(repository);
        await viewModel.InitializeAsync();
        var initialCallCount = repository.Criteria.Count;

        viewModel.SearchQuery = "sku-target";
        viewModel.SelectedCategoryId = "phones";
        viewModel.SelectedBrand = "Apple";
        viewModel.SelectedPriceBand = ProductPriceBand.From10To25Million;
        viewModel.SelectedStockStatus = ProductStatus.LowStock;
        viewModel.SelectedSortMode = ProductSortMode.StockDescending;

        await Task.Delay(180);
        Assert.Equal(initialCallCount + 5, repository.Criteria.Count);
        await Task.Delay(180);

        var criteria = repository.Criteria.Last();
        Assert.Equal("sku-target", criteria.SearchQuery);
        Assert.Equal("phones", criteria.CategoryId);
        Assert.Equal("Apple", criteria.Brand);
        Assert.Equal(ProductPriceBand.From10To25Million, criteria.PriceBand);
        Assert.Equal(ProductStatus.LowStock, criteria.StockStatus);
        Assert.Equal(ProductSortMode.StockDescending, criteria.SortMode);
        Assert.Equal(0, criteria.PageIndex);
    }

    [Fact]
    public async Task FilterChangeFromLaterPage_ResetsToFirstPageAndUpdatesMatchingTotal()
    {
        var products = Enumerable.Range(1, 8)
            .Select(index => Product($"decoy-{index}", $"Other {index}", "Other", "laptops", 30_000_000m, ProductStatus.InStock))
            .Append(Product("target", "Target Phone", "Apple", "phones", 15_000_000m, ProductStatus.LowStock))
            .ToArray();
        var viewModel = new ProductListViewModel(new FakeProductRepository(products));
        await viewModel.InitializeAsync();
        await viewModel.NextPageAsync();
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.SelectedCategoryId = "phones";
        viewModel.SelectedBrand = "Apple";
        viewModel.SelectedPriceBand = ProductPriceBand.From10To25Million;
        viewModel.SelectedStockStatus = ProductStatus.LowStock;

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(1, viewModel.TotalItems);
        Assert.Equal("target", Assert.Single(viewModel.Products).Id);
    }

    [Fact]
    public async Task OlderSearchCompletingLast_CannotReplaceNewerResults()
    {
        var repository = new CancellationIgnoringRepository();
        var viewModel = new ProductListViewModel(repository);
        var olderRequest = viewModel.InitializeAsync();
        viewModel.SearchQuery = "new query";

        await WaitUntilAsync(() => repository.Requests.Count == 2);
        repository.Complete(1, PageWithProduct("new-result"));
        await WaitUntilAsync(() => !viewModel.IsLoading && viewModel.Products.SingleOrDefault()?.Id == "new-result");
        repository.Complete(0, PageWithProduct("stale-result"));
        await olderRequest;

        Assert.Equal("new-result", Assert.Single(viewModel.Products).Id);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RequestFromPreviousVisit_CannotPublishErrorAfterRouteReentry()
    {
        var repository = new CancellationIgnoringRepository();
        var viewModel = new ProductListViewModel(repository);
        var abandonedRequest = viewModel.InitializeAsync();
        viewModel.OnNavigatedFrom();
        var currentRequest = viewModel.InitializeAsync();

        repository.Complete(1, PageWithProduct("current-result"));
        await currentRequest;
        repository.Fail(0, new InvalidOperationException("stale failure"));
        await abandonedRequest;

        Assert.Equal("current-result", Assert.Single(viewModel.Products).Id);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.IsLoading);
    }

    private static PagedResult<ProductSummary> Page(int total, int pageIndex, int itemCount)
    {
        var products = Enumerable.Range(1, itemCount)
            .Select(index => new ProductSummary(
                $"p{index}",
                $"Product {index}",
                "Brand",
                "phones",
                "Phones",
                null,
                ProductStatus.InStock,
                [new ProductVariantSummary($"v{index}", "Base", $"SKU-{index}", 100m, 3)],
                DateTimeOffset.UnixEpoch))
            .ToArray();
        return new PagedResult<ProductSummary>(products, total, pageIndex, 8);
    }

    private static ProductSummary Product(
        string id,
        string name,
        string brand,
        string category,
        decimal price,
        ProductStatus status) => new(
        id,
        name,
        brand,
        category,
        category,
        null,
        status,
        [new ProductVariantSummary($"v-{id}", "Base", $"SKU-{id}", price, status == ProductStatus.LowStock ? 2 : 8)],
        DateTimeOffset.UnixEpoch);

    private static PagedResult<ProductSummary> PageWithProduct(string id) =>
        new([Product(id, id, "Brand", "phones", 100m, ProductStatus.InStock)], 1, 0, 8);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Expected asynchronous state was not reached.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class StubProductRepository(
        Func<ProductListCriteria, Task<PagedResult<ProductSummary>>> search) : IProductRepository
    {
        public ConcurrentQueue<ProductListCriteria> Criteria { get; } = new();

        public Task<PagedResult<ProductSummary>> SearchProductsAsync(
            ProductListCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Criteria.Enqueue(criteria);
            return search(criteria);
        }

        public Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDetail?>(null);

        public Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductCreateResult>(new NotSupportedException());

        public Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductEditData?>(null);

        public Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductEditResult>(new NotSupportedException());
    }

    private sealed class RecordingProductDetailDialogService : IProductDetailDialogService
    {
        public string? ProductId { get; private set; }

        public Task ShowAsync(string productId)
        {
            ProductId = productId;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProductEditDialogService(ProductEditDialogResult result) : IProductEditDialogService
    {
        public string? ProductId { get; private set; }

        public Task<ProductEditDialogResult> ShowAsync(string productId)
        {
            ProductId = productId;
            return Task.FromResult(result);
        }
    }

    private sealed class SavingProductEditDialogService(FakeProductRepository repository) : IProductEditDialogService
    {
        public async Task<ProductEditDialogResult> ShowAsync(string productId)
        {
            var product = await repository.GetProductForEditAsync(new ProductEditQuery(productId, IncludeCostFields: false));
            if (product is null)
            {
                return new ProductEditDialogResult(ProductEditDialogOutcome.Failed);
            }

            var draft = new ProductEditDraft(
                product.Id,
                "Updated iPhone 15 Pro Max",
                product.Brand,
                product.CategoryId,
                product.ProductSku,
                product.Barcode,
                product.ModelId,
                product.ImageUrl,
                product.BasePrice,
                null,
                product.IsSerialTracked,
                product.Specifications,
                product.Variants.Select(variant => new ProductEditVariantDraft(
                    variant.Id, variant.Name, variant.Sku, variant.Barcode, variant.StorageLabel,
                    variant.ColorLabel, variant.SwatchColor, variant.SellingPrice, null, variant.Specifications)).ToArray());
            var result = await repository.UpdateProductAsync(draft);
            return result.IsSuccess
                ? new ProductEditDialogResult(ProductEditDialogOutcome.Saved, result.Product)
                : new ProductEditDialogResult(ProductEditDialogOutcome.Failed);
        }
    }

    private sealed class RecordingProductCreateDialogService(ProductCreateDialogResult result) : IProductCreateDialogService
    {
        public Task<ProductCreateDialogResult> ShowAsync() => Task.FromResult(result);
    }

    private sealed class CancellationIgnoringRepository : IProductRepository
    {
        public List<TaskCompletionSource<PagedResult<ProductSummary>>> Requests { get; } = new();

        public Task<PagedResult<ProductSummary>> SearchProductsAsync(
            ProductListCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            var request = new TaskCompletionSource<PagedResult<ProductSummary>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Requests.Add(request);
            return request.Task;
        }

        public Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDetail?>(null);

        public Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductCreateResult>(new NotSupportedException());

        public Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductEditData?>(null);

        public Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductEditResult>(new NotSupportedException());

        public void Complete(int index, PagedResult<ProductSummary> result) => Requests[index].SetResult(result);

        public void Fail(int index, Exception error) => Requests[index].SetException(error);
    }
}
