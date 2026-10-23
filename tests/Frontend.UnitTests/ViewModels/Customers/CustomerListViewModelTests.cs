using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Customers;

public sealed class CustomerListViewModelTests
{
    [Fact]
    public async Task Activate_ShowsEmptyPageAndRetryRecoversFromSafeError()
    {
        var fail = true;
        var repository = new FakeCustomerRepository((query, _) => fail
            ? Task.FromException<CustomerPage<CustomerSummary>>(new ApiException(ApiErrorKind.Network, "private details"))
            : Task.FromResult(Page(query, 0)));
        var viewModel = new CustomerListViewModel(repository);

        await viewModel.ActivateAsync();

        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Contains("kết nối", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.RetryCommand.CanExecute(null));

        fail = false;
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(PageContentState.Empty, viewModel.ContentState);
        Assert.True(viewModel.ShowEmptyState);
        Assert.Empty(viewModel.Customers);
    }

    [Fact]
    public async Task SearchAndTierFilterResetToFirstPage_AndPagingUsesServerPageMetadata()
    {
        var repository = new FakeCustomerRepository((query, _) => Task.FromResult(Page(query, 21, totalPages: 2)));
        var viewModel = new CustomerListViewModel(repository);
        await viewModel.ActivateAsync();

        viewModel.SearchText = " An ";
        viewModel.SelectedTier = "Gold";
        await viewModel.SearchCommand.ExecuteAsync(null);

        var query = repository.Queries.Last();
        Assert.Equal("An", query.Query);
        Assert.Equal("Gold", query.Tier);
        Assert.Equal(1, query.Page);
        Assert.Equal(2, viewModel.TotalPages);

        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, repository.Queries.Last().Page);
        Assert.Equal(2, viewModel.CurrentPage);
    }

    [Fact]
    public async Task ActivityFilter_DefaultsToActiveAndComposesWithSearchTierAndPaging()
    {
        var repository = new FakeCustomerRepository((query, _) => Task.FromResult(Page(query, 21, totalPages: 2)));
        var viewModel = new CustomerListViewModel(repository);

        await viewModel.ActivateAsync();
        Assert.True(repository.Queries.Last().IsActive);
        Assert.Equal(0, viewModel.SelectedActivityIndex);

        viewModel.SearchText = " Vy ";
        viewModel.SelectedTier = "Silver";
        viewModel.SelectedActivityIndex = 1;
        await viewModel.SearchCommand.ExecuteAsync(null);
        var inactiveQuery = repository.Queries.Last();
        Assert.False(inactiveQuery.IsActive);
        Assert.Equal("Vy", inactiveQuery.Query);
        Assert.Equal("Silver", inactiveQuery.Tier);
        Assert.Equal(1, inactiveQuery.Page);

        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.False(repository.Queries.Last().IsActive);
        Assert.Equal(2, repository.Queries.Last().Page);

        viewModel.SelectedActivityIndex = 2;
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.Null(repository.Queries.Last().IsActive);
    }

    [Fact]
    public async Task ClearSearchAndFilters_ReturnsToActiveCustomerDefault()
    {
        var repository = new FakeCustomerRepository((query, _) => Task.FromResult(Page(query, 1)));
        var viewModel = new CustomerListViewModel(repository);
        await viewModel.ActivateAsync();
        viewModel.SearchText = "Vy";
        viewModel.SelectedTier = "Gold";
        viewModel.SelectedActivityIndex = 2;

        await viewModel.ClearSearchCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(string.Empty, viewModel.SelectedTier);
        Assert.Equal(0, viewModel.SelectedActivityIndex);
        Assert.True(repository.Queries.Last().IsActive);
    }

    [Fact]
    public async Task SearchTextChangesAreDebounced()
    {
        var repository = new FakeCustomerRepository((query, _) => Task.FromResult(Page(query, 1)));
        var viewModel = new CustomerListViewModel(repository);
        await viewModel.ActivateAsync();
        var initialCount = repository.SearchCount;

        viewModel.SearchText = "V";
        viewModel.SearchText = "Vy";
        viewModel.SearchText = "Vy N";
        await Task.Delay(450);

        Assert.Equal(initialCount + 1, repository.SearchCount);
        Assert.Equal("Vy N", repository.Queries.Last().Query);
    }

    [Fact]
    public async Task StaleSearchResponseCannotReplaceLatestQuery()
    {
        var oldResponse = new TaskCompletionSource<CustomerPage<CustomerSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<CustomerPage<CustomerSummary>>? newResponse = null;
        var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new FakeCustomerRepository((query, _) => query.Query switch
        {
            "old" => StartOld(),
            "new" => StartNew(),
            _ => Task.FromResult(Page(query, 1))
        });
        var viewModel = new CustomerListViewModel(repository);
        await viewModel.ActivateAsync();

        viewModel.SearchText = "old";
        await Task.Delay(350);
        await oldStarted.Task;
        viewModel.SearchText = "new";
        await Task.Delay(350);
        await newStarted.Task;
        var newestCustomer = Summary("new result");
        newResponse!.SetResult(new CustomerPage<CustomerSummary>([newestCustomer], 1, 20, 1, 1));
        await Task.Delay(30);
        oldResponse.SetResult(new CustomerPage<CustomerSummary>([Summary("stale result")], 1, 20, 1, 1));
        await Task.Delay(30);

        Assert.Equal("new result", Assert.Single(viewModel.Customers).FullName);

        Task<CustomerPage<CustomerSummary>> StartOld()
        {
            oldStarted.TrySetResult();
            return oldResponse.Task;
        }

        Task<CustomerPage<CustomerSummary>> StartNew()
        {
            newStarted.TrySetResult();
            newResponse = new TaskCompletionSource<CustomerPage<CustomerSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            return newResponse.Task;
        }
    }

    [Fact]
    public async Task CreateEntryAndDeactivationConfirmationDoNotWriteUntilConfirmed()
    {
        var activeCustomer = Summary("active") with { IsActive = true };
        var isInactive = false;
        var repository = new FakeCustomerRepository((query, _) => Task.FromResult(new CustomerPage<CustomerSummary>(
            [isInactive ? activeCustomer with { IsActive = false } : activeCustomer], 1, 20, 1, 1)));
        repository.OnDeactivate = _ => isInactive = true;
        var viewModel = new CustomerListViewModel(repository);
        await viewModel.ActivateAsync();

        viewModel.OpenCreateDialogCommand.Execute(null);
        Assert.True(viewModel.IsCreateDialogOpen);
        viewModel.CloseCreateDialogCommand.Execute(null);
        Assert.False(viewModel.IsCreateDialogOpen);

        viewModel.RequestDeactivationCommand.Execute(activeCustomer);
        Assert.True(viewModel.IsDeactivationConfirmationOpen);
        Assert.Equal(0, repository.DeactivateCount);
        viewModel.CancelDeactivationCommand.Execute(null);
        Assert.False(viewModel.IsDeactivationConfirmationOpen);
        Assert.Equal(0, repository.DeactivateCount);

        viewModel.RequestDeactivationCommand.Execute(activeCustomer);
        await viewModel.ConfirmDeactivationCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.DeactivateCount);
        Assert.False(viewModel.IsDeactivationConfirmationOpen);
        Assert.False(Assert.Single(viewModel.Customers).IsActive);
    }

    [Fact]
    public async Task DeactivationFailure_KeepsConfirmationOpenAndShowsSafeError()
    {
        var activeCustomer = Summary("active") with { IsActive = true };
        var repository = new FakeCustomerRepository((query, _) => Task.FromResult(new CustomerPage<CustomerSummary>([activeCustomer], 1, 20, 1, 1)))
        {
            DeactivateException = new ApiException(ApiErrorKind.Network, "private database detail")
        };
        var viewModel = new CustomerListViewModel(repository);
        await viewModel.ActivateAsync();
        viewModel.RequestDeactivationCommand.Execute(activeCustomer);

        await viewModel.ConfirmDeactivationCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDeactivationConfirmationOpen);
        Assert.Contains("Không thể kết nối", viewModel.ActionErrorMessage);
        Assert.DoesNotContain("private", viewModel.ActionErrorMessage);
        Assert.True(Assert.Single(viewModel.Customers).IsActive);
        Assert.Equal(1, repository.DeactivateCount);
    }

    private static CustomerPage<CustomerSummary> Page(CustomerQuery query, int totalCount, int totalPages = 1)
    {
        var items = totalCount == 0 || query.Page > totalPages ? [] : new[] { Summary("customer") };
        return new(items, query.Page, query.PageSize, totalCount, totalPages);
    }

    private static CustomerSummary Summary(string name) => new(
        Guid.NewGuid(), name, "0901", 10, "Gold", true, DateTime.UtcNow, 2, 100m, null);

    private sealed class FakeCustomerRepository(Func<CustomerQuery, CancellationToken, Task<CustomerPage<CustomerSummary>>> search) : ICustomerRepository
    {
        public int SearchCount { get; private set; }
        public int DeactivateCount { get; private set; }
        public Action<Guid>? OnDeactivate { get; set; }
        public Exception? DeactivateException { get; init; }
        public List<CustomerQuery> Queries { get; } = [];

        public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default)
        {
            SearchCount++;
            Queries.Add(query);
            return search(query, cancellationToken);
        }

        public Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CustomerProfile?>(null);
        public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            DeactivateCount++;
            if (DeactivateException is not null) return Task.FromException(DeactivateException);
            OnDeactivate?.Invoke(customerId);
            return Task.CompletedTask;
        }
        public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
