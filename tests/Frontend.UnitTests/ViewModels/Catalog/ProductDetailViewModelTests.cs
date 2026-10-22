using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductDetailViewModelTests
{
    [Fact]
    public async Task ProductDetailViewModel_Load_FillsViewer()
    {
        // Arrange
        var product = Detail(ProductId) with { Images = [new(Guid.NewGuid(), null, "/image.png", "image/png", 1, 0)] };
        var viewModel = Create(new FakeProductRepository((_, _) => Task.FromResult<ProductDetail?>(product)));
        viewModel.SetRoute(ProductId, null, null);
        // Act
        await viewModel.ActivateAsync();
        // Assert
        Assert.Equal("/image.png", viewModel.ImageViewer.SelectedUrl);
    }

    [Fact]
    public async Task ConstructorDoesNoIo_AndActivationLoadsOnlyConfiguredId()
    {
        var repository = new FakeProductRepository((_, _) => Task.FromResult<ProductDetail?>(Detail(ProductId)));
        var viewModel = Create(repository);
        Assert.Equal(0, repository.DetailCount);
        viewModel.SetRoute(ProductId, new ProductQuery { Query = "pixel", Page = 2 }, null);

        await viewModel.ActivateAsync();

        Assert.Equal(1, repository.DetailCount);
        Assert.Equal(ProductId, repository.RequestedId);
        Assert.Equal(ProductId, viewModel.Product?.Id);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task MissingProductAndServerErrorsHaveDistinctStates_AndRetrySameId()
    {
        var fail = true;
        var repository = new FakeProductRepository((id, _) => fail
            ? Task.FromException<ProductDetail?>(new ApiException(ApiErrorKind.Http, "server", 500))
            : Task.FromResult<ProductDetail?>(Detail(id)));
        var viewModel = Create(repository);
        viewModel.SetRoute(ProductId, null, null);

        await viewModel.ActivateAsync();
        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Null(viewModel.Product);
        Assert.True(viewModel.RetryCommand.CanExecute(null));

        fail = false;
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal(ProductId, repository.RequestedId);
        Assert.Equal(ProductId, viewModel.Product?.Id);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);

        var missing = Create(new FakeProductRepository((_, _) => Task.FromResult<ProductDetail?>(null)));
        missing.SetRoute(ProductId, null, null);
        await missing.ActivateAsync();
        Assert.True(missing.IsNotFound);
        Assert.Equal(PageContentState.Empty, missing.ContentState);
    }

    [Fact]
    public async Task MismatchedIdAndLateResponseNeverExposeAnotherProduct()
    {
        var mismatch = Create(new FakeProductRepository((_, _) => Task.FromResult<ProductDetail?>(Detail(Guid.NewGuid()))));
        mismatch.SetRoute(ProductId, null, null);
        await mismatch.ActivateAsync();
        Assert.Equal(PageContentState.Error, mismatch.ContentState);
        Assert.Null(mismatch.Product);

        var pending = new TaskCompletionSource<ProductDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new FakeProductRepository((_, _) => pending.Task);
        var viewModel = Create(repository);
        viewModel.SetRoute(ProductId, null, null);
        var load = viewModel.ActivateAsync();
        viewModel.Deactivate();
        pending.SetResult(Detail(ProductId));
        await load;
        Assert.Null(viewModel.Product);
    }

    [Fact]
    public async Task BackCreatesListRouteWithAppliedCriteriaAndFocusHint()
    {
        var navigation = new NavigationService(new AllowingPreparer());
        var criteria = new ProductQuery { Query = "Pixel", Page = 4, PageSize = 50, Sort = "name:asc" };
        var repository = new FakeProductRepository((id, _) => Task.FromResult<ProductDetail?>(Detail(id)));
        var viewModel = new ProductDetailViewModel(repository, navigation, new InMemorySessionStore(), new RecordingProductCommands(), new ScriptedDialogService(), new StubSpecRepository());
        viewModel.SetRoute(ProductId, criteria, ProductId);
        await viewModel.ActivateAsync();

        viewModel.BackCommand.Execute(null);

        Assert.Equal(AppRouteKind.ProductList, navigation.CurrentRoute.Kind);
        Assert.Equal(criteria.Query, navigation.CurrentRoute.ReturnQuery?.Query);
        Assert.Equal(criteria.Page, navigation.CurrentRoute.ReturnQuery?.Page);
        Assert.Equal(ProductId, navigation.CurrentRoute.FocusProductId);
    }

    private static Guid ProductId => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static ProductDetailViewModel Create(FakeProductRepository repository) =>
        new(repository, new NavigationService(new AllowingPreparer()), new InMemorySessionStore(), new RecordingProductCommands(), new ScriptedDialogService(), new StubSpecRepository());

    private static ProductDetail Detail(Guid id) => new(
        id, Guid.NewGuid(), "Mobile", "Test phone", "SKU-1", null, "Brand", 100m, 80m,
        null, null, new Dictionary<string, JsonElement>(), false, true, DateTime.UtcNow, null, [], []);

    private sealed class FakeProductRepository(Func<Guid, CancellationToken, Task<ProductDetail?>> detail) : IProductRepository
    {
        public int DetailCount { get; private set; }
        public Guid RequestedId { get; private set; }
        public Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductPage([], query.Page, query.PageSize, 0, 0));

        public Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DetailCount++;
            RequestedId = id;
            return detail(id, cancellationToken);
        }
    }

    private sealed class AllowingPreparer : IDestinationPreparer
    {
        public bool Prepare(AppRoute route) => true;
    }
}
