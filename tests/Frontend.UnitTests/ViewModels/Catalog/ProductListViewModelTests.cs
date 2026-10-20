using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductListViewModelTests
{
    [Fact]
    public async Task ConstructorDoesNoIo_DraftIsNotSubmitted_AndSearchBuildsValidatedCriteria()
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query, count: 1)));
        var categories = new FakeCategoryRepository();
        var viewModel = CreateViewModel(repository, categories);

        Assert.Equal(0, repository.SearchCount);
        Assert.Equal(0, categories.RequestCount);
        await viewModel.ActivateAsync();
        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(1, categories.RequestCount);

        viewModel.SearchText = "  Pixel & Pro  ";
        viewModel.BrandText = " Google ";
        viewModel.ActiveFilter = "inactive";
        viewModel.MinPriceText = "1.234,50";
        viewModel.MaxPriceText = "2.000,00";
        viewModel.SpecsText = "ram=16\nhas_5g=true";
        viewModel.SelectedCategoryId = Guid.NewGuid();
        Assert.Equal(1, repository.SearchCount);
        viewModel.SelectedSort = "price:asc";
        Assert.Equal(2, repository.SearchCount);
        viewModel.SelectedPageSize = 50;
        Assert.Equal(3, repository.SearchCount);

        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.Equal(4, repository.SearchCount);

        var query = repository.Queries.Last();
        Assert.Equal("Pixel & Pro", query.Query);
        Assert.Equal("Google", query.Brand);
        Assert.False(query.IsActive);
        Assert.Equal(1234.50m, query.MinPrice);
        Assert.Equal(2000m, query.MaxPrice);
        Assert.Equal(["ram:16", "has_5g:true"], query.Specs);
        Assert.Equal("price:asc", query.Sort);
        Assert.Equal(1, query.Page);
        Assert.Equal(50, query.PageSize);
        Assert.NotNull(query.CategoryId);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task InvalidDraftDoesNotRequest_AndResetSubmitsDefaultsAtPageOne()
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query, page: 3, totalPages: 4)));
        var viewModel = CreateViewModel(repository);
        await viewModel.ActivateAsync();

        viewModel.MinPriceText = "900";
        viewModel.MaxPriceText = "100";
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.SearchCount);
        Assert.True(viewModel.HasValidationError);

        await viewModel.ResetFiltersCommand.ExecuteAsync(null);
        var reset = repository.Queries.Last();
        Assert.Equal(2, repository.SearchCount);
        Assert.Null(reset.Query);
        Assert.Null(reset.Brand);
        Assert.Null(reset.MinPrice);
        Assert.Null(reset.MaxPrice);
        Assert.Null(reset.CategoryId);
        Assert.Null(reset.IsActive);
        Assert.Empty(reset.Specs);
        Assert.Equal("createdAt:desc", reset.Sort);
        Assert.Equal(20, reset.PageSize);
        Assert.Equal(1, reset.Page);
    }

    [Fact]
    public async Task PagingUsesServerMetadataAndOutOfRangeOffersFirstPageRecovery()
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(
            query.Page == 3 ? Page(query, page: 3, totalPages: 2, count: 21) : Page(query, totalPages: 2, count: 21)));
        var viewModel = CreateViewModel(repository);
        await viewModel.ActivateAsync();
        Assert.Equal(2, viewModel.TotalPages);

        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, repository.Queries.Last().Page);
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.AppliedCriteria = viewModel.AppliedCriteria with { Page = 3 };
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsOutOfRange);
        Assert.Contains("trang đầu", viewModel.ResultContext, StringComparison.OrdinalIgnoreCase);

        await viewModel.FirstPageCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.Queries.Last().Page);
        Assert.False(viewModel.IsOutOfRange);
    }

    [Fact]
    public async Task FailedNewQueryKeepsClearlyLabeledSnapshot_AndRetryUsesRequestedQuery()
    {
        var fail = false;
        var repository = new FakeProductRepository((query, _) => fail
            ? Task.FromException<ProductPage>(new ApiException(ApiErrorKind.Http, "server", 500))
            : Task.FromResult(Page(query, count: query.Query is null ? 5 : 1)));
        var viewModel = CreateViewModel(repository);
        await viewModel.ActivateAsync();
        var previous = viewModel.Snapshot;
        fail = true;
        viewModel.SearchText = "requested";
        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Same(previous, viewModel.Snapshot);
        Assert.True(viewModel.HasResultsForPreviousQuery);
        Assert.Equal("requested", viewModel.RequestedCriteria?.Query);
        Assert.True(viewModel.RetryCommand.CanExecute(null));

        fail = false;
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal("requested", repository.Queries.Last().Query);
        Assert.False(viewModel.HasResultsForPreviousQuery);
    }

    [Fact]
    public async Task ForbiddenAndSessionReplacementClearDataAndIgnoreLateResponse()
    {
        var failForbidden = false;
        var waitForLateResponse = false;
        var lateResponse = new TaskCompletionSource<ProductPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = lateResponse;
        var session = new InMemorySessionStore();
        var repository = new FakeProductRepository((query, _) =>
        {
            if (failForbidden) return Task.FromException<ProductPage>(new ApiException(ApiErrorKind.Http, "forbidden", 403));
            if (waitForLateResponse)
            {
                started.TrySetResult();
                return pendingResponse.Task;
            }
            return Task.FromResult(Page(query, count: 2));
        });
        var viewModel = CreateViewModel(repository, new FakeCategoryRepository(), session);
        await viewModel.ActivateAsync();
        failForbidden = true;
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.Null(viewModel.Snapshot);
        Assert.Equal(PageContentState.Error, viewModel.ContentState);

        failForbidden = false;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.Snapshot);
        waitForLateResponse = true;
        viewModel.SearchText = "late";
        var request = viewModel.SearchCommand.ExecuteAsync(null);
        await started.Task;
        session.SetAccessToken("replacement-token");
        Assert.Null(viewModel.Snapshot);
        Assert.Null(viewModel.AppliedCriteria.Query);
        Assert.Equal(PageContentState.Idle, viewModel.ContentState);
        lateResponse.SetResult(Page(repository.Queries.Last(), count: 4));
        await request;
        Assert.Null(viewModel.Snapshot);

        waitForLateResponse = false;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.Snapshot);
        waitForLateResponse = true;
        pendingResponse = new TaskCompletionSource<ProductPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.SearchText = "cleared-session";
        var pendingRequest = viewModel.SearchCommand.ExecuteAsync(null);
        session.Clear();
        Assert.Null(viewModel.Snapshot);
        Assert.Null(viewModel.AppliedCriteria.Query);
        pendingResponse.SetResult(Page(repository.Queries.Last(), count: 6));
        await pendingRequest;
        Assert.Null(viewModel.Snapshot);
    }

    [Fact]
    public async Task BadRequestExposesFieldErrorsWithoutRenderingRawBody()
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>
        {
            ["minPrice"] = ["invalid"]
        };
        var repository = new FakeProductRepository((_, _) => Task.FromException<ProductPage>(new ApiException(
            ApiErrorKind.Http, "raw body should stay hidden", 400, fieldErrors: fields)));
        var viewModel = CreateViewModel(repository);

        await viewModel.ActivateAsync();

        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Contains("Catalog API", viewModel.ErrorMessage);
        Assert.DoesNotContain("raw body", viewModel.ErrorMessage);
        Assert.Contains("minPrice", viewModel.FieldErrors.Keys);
    }

    [Theory]
    [InlineData("name:asc")]
    [InlineData("name:desc")]
    [InlineData("price:asc")]
    [InlineData("price:desc")]
    [InlineData("createdAt:asc")]
    [InlineData("createdAt:desc")]
    public async Task SelectingAnySupportedSortImmediatelyRequestsAppliedCriteria(string sort)
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query, count: 1)));
        var viewModel = CreateViewModel(repository);
        await viewModel.ActivateAsync();

        viewModel.SelectedSort = sort;

        Assert.Equal(sort == "createdAt:desc" ? 1 : 2, repository.SearchCount);
        Assert.Equal(sort, repository.Queries.Last().Sort);
        Assert.Equal(1, repository.Queries.Last().Page);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public async Task SelectingSupportedPageSizeImmediatelyResetsToFirstPage(int pageSize)
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query, count: 1)));
        var viewModel = CreateViewModel(repository);
        await viewModel.ActivateAsync();

        viewModel.SelectedPageSize = pageSize;

        Assert.Equal(pageSize, repository.Queries.Last().PageSize);
        Assert.Equal(1, repository.Queries.Last().Page);
    }

    [Fact]
    public async Task CategoryLookupFailureIsIndependentAndCanBeRetried()
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query, count: 1)));
        var categories = new FakeCategoryRepository(failFirst: true);
        var viewModel = CreateViewModel(repository, categories);
        await viewModel.ActivateAsync();

        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.True(viewModel.HasCategoryError);
        Assert.Equal(1, repository.SearchCount);

        await viewModel.RetryCategoriesCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasCategoryError);
        Assert.Single(viewModel.Categories);
        Assert.Equal(2, categories.RequestCount);
        Assert.Equal(1, repository.SearchCount);
    }

    [Theory]
    [InlineData(ApiErrorKind.Network, "kết nối Catalog API")]
    [InlineData(ApiErrorKind.Timeout, "quá thời gian chờ")]
    [InlineData(ApiErrorKind.InvalidResponse, "không đúng định dạng")]
    public async Task TransportAndContractFailuresRemainErrorsAndKeepOldSnapshot(ApiErrorKind kind, string safeText)
    {
        var requests = 0;
        var repository = new FakeProductRepository((query, _) => Interlocked.Increment(ref requests) == 1
            ? Task.FromResult(Page(query, count: 1))
            : Task.FromException<ProductPage>(new ApiException(kind, "secret details")));
        var viewModel = CreateViewModel(repository);
        await viewModel.ActivateAsync();
        var snapshot = viewModel.Snapshot;
        viewModel.SearchText = "different";

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Same(snapshot, viewModel.Snapshot);
        Assert.True(viewModel.HasResultsForPreviousQuery);
        Assert.Contains(safeText, viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpeningCardPassesProductIdAndAppliedCriteriaToDetailRoute()
    {
        var navigation = new NavigationService(new AllowingPreparer());
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query, totalPages: 3, count: 45)));
        var viewModel = new ProductListViewModel(repository, new FakeCategoryRepository(), new InMemorySessionStore(), navigation);
        viewModel.SetRoute(new ProductQuery { Query = "applied", Page = 2 }, null);
        await viewModel.ActivateAsync();
        var productId = Guid.NewGuid();

        viewModel.OpenProductCommand.Execute(new ProductSummary(productId, "Phone", "SKU", "Brand",
            Guid.NewGuid(), "Category", 10, 10, 0, false, true, null));

        Assert.Equal(AppRouteKind.ProductDetail, navigation.CurrentRoute.Kind);
        Assert.Equal(productId, navigation.CurrentRoute.ProductId);
        Assert.Equal("applied", navigation.CurrentRoute.ReturnQuery?.Query);
        Assert.Equal(2, navigation.CurrentRoute.ReturnQuery?.Page);
        Assert.Equal(productId, navigation.CurrentRoute.FocusProductId);
        Assert.Equal(1, repository.SearchCount);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SalesStaff", false)]
    public void CanManage_WithRole_ReturnsExpectedValue(string role, bool expected)
    {
        var session = new InMemorySessionStore();
        session.SetSession("t", "r", "admin", "Admin", role);
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query)));

        var viewModel = CreateViewModel(repository, session: session);

        Assert.Equal(expected, viewModel.CanManage);
    }

    [Fact]
    public void CanManage_AnonymousSession_ReturnsFalse()
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query)));

        var viewModel = CreateViewModel(repository);

        Assert.False(viewModel.CanManage);
    }

    [Fact]
    public void CanManage_TurnsFalseAfterSessionClear()
    {
        var session = new InMemorySessionStore();
        session.SetSession("t", "r", "admin", "Admin", "Admin");
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query)));
        var viewModel = CreateViewModel(repository, session: session);
        Assert.True(viewModel.CanManage);

        session.Clear();

        Assert.False(viewModel.CanManage);
    }

    [Fact]
    public async Task CreateProductCommand_WithAdmin_NavigatesToProductCreateCarryingAppliedQuery()
    {
        var navigation = new NavigationService(new AllowingPreparer());
        navigation.Navigate(AppRoute.Products());
        var session = new InMemorySessionStore();
        session.SetSession("t", "r", "admin", "Admin", "Admin");
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query)));
        var viewModel = new ProductListViewModel(repository, new FakeCategoryRepository(), session, navigation);
        viewModel.SetRoute(new ProductQuery { Query = "laptop", Page = 2 }, null);
        await viewModel.ActivateAsync();

        viewModel.CreateProductCommand.Execute(null);

        Assert.Equal(AppRouteKind.ProductCreate, navigation.CurrentRoute.Kind);
        Assert.Equal("laptop", navigation.CurrentRoute.ReturnQuery?.Query);
        Assert.Equal(2, navigation.CurrentRoute.ReturnQuery?.Page);
    }

    [Fact]
    public async Task CreateProductCommand_WithSalesStaff_DoesNotNavigate()
    {
        var navigation = new NavigationService(new AllowingPreparer());
        navigation.Navigate(AppRoute.Products());
        var session = new InMemorySessionStore();
        session.SetSession("t", "r", "cashier", "Cashier", "SalesStaff");
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query)));
        var viewModel = new ProductListViewModel(repository, new FakeCategoryRepository(), session, navigation);
        viewModel.SetRoute(new ProductQuery { Query = "laptop" }, null);
        await viewModel.ActivateAsync();

        viewModel.CreateProductCommand.Execute(null);

        Assert.Equal(AppRouteKind.ProductList, navigation.CurrentRoute.Kind);
    }

    [Fact]
    public void ClearFocusRequest_SetsFocusProductIdToNull()
    {
        var repository = new FakeProductRepository((query, _) => Task.FromResult(Page(query)));
        var viewModel = CreateViewModel(repository);
        var focusId = Guid.NewGuid();
        viewModel.SetRoute(new ProductQuery(), focusId);
        Assert.Equal(focusId, viewModel.FocusProductId);

        viewModel.ClearFocusRequest();

        Assert.Null(viewModel.FocusProductId);
    }

    private static ProductListViewModel CreateViewModel(
        FakeProductRepository products,
        FakeCategoryRepository? categories = null,
        InMemorySessionStore? session = null) =>
        new(products, categories ?? new FakeCategoryRepository(), session ?? new InMemorySessionStore(),
            new NavigationService(new AllowingPreparer()));

    private static ProductPage Page(ProductQuery query, int? page = null, int totalPages = 1, int count = 0)
    {
        var currentPage = page ?? query.Page;
        var items = count == 0 || currentPage > totalPages ? [] : new[]
        {
            new ProductSummary(Guid.NewGuid(), "Phone", "SKU-1", "Brand", Guid.NewGuid(),
                "Category", 100m, 100m, 0, false, true, null)
        };
        return new ProductPage(items, currentPage, query.PageSize, count, totalPages);
    }

    private sealed class FakeProductRepository(Func<ProductQuery, CancellationToken, Task<ProductPage>> search) : IProductRepository
    {
        public int SearchCount { get; private set; }
        public List<ProductQuery> Queries { get; } = [];

        public Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default)
        {
            SearchCount++;
            Queries.Add(query);
            return search(query, cancellationToken);
        }

        public Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDetail?>(null);
    }

    private sealed class FakeCategoryRepository(bool failFirst = false) : ICategoryRepository
    {
        public int RequestCount { get; private set; }
        public Task<IReadOnlyList<CategoryOption>> GetTreeAsync(CancellationToken cancellationToken = default)
        {
            RequestCount++;
            if (failFirst && RequestCount == 1)
                return Task.FromException<IReadOnlyList<CategoryOption>>(new ApiException(ApiErrorKind.Http, "categories", 500));
            return Task.FromResult<IReadOnlyList<CategoryOption>>([new CategoryOption(Guid.NewGuid(), null, "Mobile", "Mobile")]);
        }
    }

    private sealed class AllowingPreparer : IDestinationPreparer
    {
        public bool Prepare(AppRoute route) => true;
    }
}
