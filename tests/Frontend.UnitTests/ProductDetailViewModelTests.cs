using System.Collections.Concurrent;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Identity;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductDetailViewModelTests
{
    [Fact]
    public async Task Load_ReportsLoadingThenPopulatedDetail()
    {
        var completion = new TaskCompletionSource<ProductDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = CreateViewModel(new StubRepository((_, _) => completion.Task));

        var load = viewModel.LoadAsync("phone-1");

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.IsUnavailable);
        completion.SetResult(Detail("phone-1"));
        await load;

        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.IsPopulated);
        Assert.Equal("phone-1", viewModel.ProductId);
        Assert.Equal("phone-1", viewModel.Detail?.Id);
    }

    [Fact]
    public async Task Load_MarksMissingProductUnavailableWithoutError()
    {
        var viewModel = CreateViewModel(new StubRepository((_, _) => Task.FromResult<ProductDetail?>(null)));

        await viewModel.LoadAsync("missing");

        Assert.True(viewModel.IsUnavailable);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.Detail);
    }

    [Fact]
    public async Task Retry_RequestsTheSameProductAfterError()
    {
        var requestedIds = new ConcurrentQueue<string>();
        var call = 0;
        var repository = new StubRepository((id, _) =>
        {
            requestedIds.Enqueue(id);
            return Interlocked.Increment(ref call) == 1
                ? Task.FromException<ProductDetail?>(new InvalidOperationException("offline"))
                : Task.FromResult<ProductDetail?>(Detail(id));
        });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync("phone-1");
        Assert.True(viewModel.IsError);
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "phone-1", "phone-1" }, requestedIds.ToArray());
        Assert.True(viewModel.IsPopulated);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public async Task NewLoad_CancelsAndSuppressesOlderRequestThatCompletesLast()
    {
        var repository = new ControlledRepository();
        var viewModel = CreateViewModel(repository);
        var oldLoad = viewModel.LoadAsync("older");
        var newLoad = viewModel.LoadAsync("newer");

        repository.Complete(1, Detail("newer"));
        await newLoad;
        repository.Complete(0, Detail("older"));
        await oldLoad;

        Assert.Equal("newer", viewModel.Detail?.Id);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public async Task CancelLoad_SuppressesAnIgnoringRepositoryResult()
    {
        var repository = new ControlledRepository();
        var viewModel = CreateViewModel(repository);
        var pending = viewModel.LoadAsync("phone-1");

        viewModel.CancelLoad();
        repository.Complete(0, Detail("phone-1"));
        await pending;

        Assert.Null(viewModel.Detail);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public async Task SelectingVariant_UpdatesItsEntireDataTupleAndRejectsUnknownVariant()
    {
        var detail = Detail("phone-1") with
        {
            Variants =
            [
                Variant("blue", "SKU-BLUE", "Titan Xanh", "256GB", 100m, 3, 20m, 80m),
                Variant("natural", "SKU-NATURAL", "Titan Tự nhiên", "512GB", 150m, 1, 40m, 110m)
            ]
        };
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview cost reader", new HashSet<string> { ProductDetailViewModel.CostReadCapability }));
        var viewModel = new ProductDetailViewModel(new StubRepository((_, _) => Task.FromResult<ProductDetail?>(detail)), session);
        await viewModel.LoadAsync("phone-1");

        Assert.Equal("blue", viewModel.SelectedVariantId);
        Assert.False(viewModel.SelectVariant("nonexistent-combination"));
        Assert.Equal("blue", viewModel.SelectedVariantId);
        Assert.True(viewModel.SelectVariant("natural"));

        Assert.Equal("SKU-NATURAL", viewModel.CurrentSku);
        Assert.Equal(150m, viewModel.CurrentSellingPrice);
        Assert.Equal(1, viewModel.CurrentAvailableQuantity);
        Assert.Equal(40m, viewModel.AverageCost);
        Assert.Equal(110m, viewModel.EstimatedMargin);
        Assert.Equal(ProductStatus.InStock, viewModel.CurrentStatus);
        Assert.Equal("natural", viewModel.SelectedVariant?.Id);
        Assert.Contains(viewModel.CurrentSpecifications, item => item.Name == "Cấu hình" && item.Value == "512GB • Titan Tự nhiên");
    }

    [Fact]
    public async Task SingleVariantDetail_DoesNotShowVariantSelector()
    {
        var viewModel = CreateViewModel(new StubRepository((_, _) => Task.FromResult<ProductDetail?>(Detail("phone-1"))));

        await viewModel.LoadAsync("phone-1");

        Assert.False(viewModel.IsVariantSelectorVisible);
        Assert.Equal("v1", viewModel.SelectedVariantId);
    }

    [Fact]
    public async Task CostValuesAreAvailableOnlyForCapabilityAndNeverCalculatedByViewModel()
    {
        var permitted = new CurrentSessionProvider();
        permitted.SetAuthenticated(new SessionSnapshot("preview cost reader", new HashSet<string> { ProductDetailViewModel.CostReadCapability }));
        var allowedVm = new ProductDetailViewModel(
            new StubRepository((_, _) => Task.FromResult<ProductDetail?>(FinancialDetail())),
            permitted);

        await allowedVm.LoadAsync("phone-1");

        Assert.True(allowedVm.IsCostPanelVisible);
        Assert.Equal(67m, allowedVm.AverageCost);
        Assert.Equal(31m, allowedVm.EstimatedMargin);

        var denied = new CurrentSessionProvider();
        denied.SetAuthenticated(new SessionSnapshot("preview without cost", new HashSet<string>()));
        var deniedVm = new ProductDetailViewModel(
            new StubRepository((_, _) => Task.FromResult<ProductDetail?>(FinancialDetail())),
            denied);

        await deniedVm.LoadAsync("phone-1");

        Assert.False(deniedVm.IsCostPanelVisible);
        Assert.Null(deniedVm.AverageCost);
        Assert.Null(deniedVm.EstimatedMargin);
        Assert.Null(deniedVm.SelectedVariant?.AverageCost);
        Assert.Null(deniedVm.SelectedVariant?.EstimatedMargin);
        Assert.All(deniedVm.Detail!.Variants, variant =>
        {
            Assert.Null(variant.AverageCost);
            Assert.Null(variant.EstimatedMargin);
        });
    }

    [Fact]
    public async Task AuthorizedSessionWithMissingCost_DoesNotInventFinancialValues()
    {
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview cost reader", new HashSet<string> { ProductDetailViewModel.CostReadCapability }));
        var detail = FinancialDetail() with
        {
            Variants = [Variant("v1", "SKU-1", "Base", null, 98m, 2, null, null)]
        };
        var viewModel = new ProductDetailViewModel(new StubRepository((_, _) => Task.FromResult<ProductDetail?>(detail)), session);

        await viewModel.LoadAsync("phone-1");

        Assert.Null(viewModel.AverageCost);
        Assert.Null(viewModel.EstimatedMargin);
        Assert.False(viewModel.IsCostPanelVisible);
    }

    [Fact]
    public async Task LosingCostCapability_RemovesValuesFromBoundDetailAndCanRestoreOnSessionChange()
    {
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview cost reader", new HashSet<string> { ProductDetailViewModel.CostReadCapability }));
        var viewModel = new ProductDetailViewModel(new StubRepository((_, _) => Task.FromResult<ProductDetail?>(FinancialDetail())), session);
        await viewModel.LoadAsync("phone-1");

        session.SetAuthenticated(new SessionSnapshot("preview without cost", new HashSet<string>()));
        Assert.Null(viewModel.SelectedVariant?.AverageCost);
        Assert.Null(viewModel.AverageCost);
        Assert.False(viewModel.IsCostPanelVisible);

        session.SetAuthenticated(new SessionSnapshot("preview cost reader", new HashSet<string> { ProductDetailViewModel.CostReadCapability }));
        Assert.Equal(67m, viewModel.AverageCost);
        Assert.True(viewModel.IsCostPanelVisible);
    }

    private static ProductDetail Detail(string id) => new(
        id,
        "Phone",
        "Brand",
        null,
        null,
        null,
        ProductStatus.InStock,
        [],
        [Variant("v1", "SKU-1", "Base", null, 100m, 2, null, null)],
        true);

    private static ProductDetail FinancialDetail() => Detail("phone-1") with
    {
        Variants = [Variant("v1", "SKU-1", "Base", null, 98m, 2, 67m, 31m)]
    };

    private static ProductDetailVariant Variant(
        string id,
        string sku,
        string name,
        string? storage,
        decimal price,
        int quantity,
        decimal? cost,
        decimal? margin) => new(
        id,
        name,
        sku,
        storage,
        name,
        null,
        price,
        quantity,
        quantity == 0 ? ProductStatus.OutOfStock : ProductStatus.InStock,
        [new ProductSpecification("Cấu hình", storage is null ? name : $"{storage} • {name}")],
        cost,
        margin);

    private static ProductDetailViewModel CreateViewModel(IProductRepository repository) =>
        new(repository, new CurrentSessionProvider());

    private sealed class StubRepository(Func<string, CancellationToken, Task<ProductDetail?>> getDetail) : IProductRepository
    {
        public Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default) =>
            getDetail(productId, cancellationToken);

        public Task<PagedResult<ProductSummary>> SearchProductsAsync(ProductListCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ProductSummary>([], 0, 0, 8));

        public Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductCreateResult>(new NotSupportedException());

        public Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductEditData?>(null);

        public Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductEditResult>(new NotSupportedException());
    }

    private sealed class ControlledRepository : IProductRepository
    {
        private readonly List<TaskCompletionSource<ProductDetail?>> _requests = [];

        public Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default)
        {
            var request = new TaskCompletionSource<ProductDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(request);
            return request.Task;
        }

        public void Complete(int index, ProductDetail result) => _requests[index].SetResult(result);

        public Task<PagedResult<ProductSummary>> SearchProductsAsync(ProductListCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ProductSummary>([], 0, 0, 8));

        public Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductCreateResult>(new NotSupportedException());

        public Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductEditData?>(null);

        public Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductEditResult>(new NotSupportedException());
    }
}
