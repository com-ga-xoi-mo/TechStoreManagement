using TechStore.Client.Controls;
using TechStore.Client.Models.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.ViewModels.Orders;

public sealed class OrderListViewModelTests
{
    private sealed record Fixture(
        OrderListViewModel ViewModel,
        FakeOrderRepository Orders,
        FakeNavigation Navigation,
        FakeDialogService Dialogs,
        FakeDetailPresenter Presenter);

    private static Fixture Create(FakeOrderRepository? orders = null)
    {
        orders ??= new FakeOrderRepository();
        var navigation = new FakeNavigation();
        var dialogs = new FakeDialogService();
        var presenter = new FakeDetailPresenter();
        return new Fixture(new OrderListViewModel(orders, navigation, dialogs, presenter), orders, navigation, dialogs, presenter);
    }

    // ---------- loading ----------

    [Fact]
    public async Task Constructor_DoesNoIo_AndActivationLoadsTheFirstPageWithDefaults()
    {
        // Arrange
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(Orders.Page(q, count: 3, total: 45, totalPages: 3)) });
        Assert.Equal(0, f.Orders.SearchCount);

        // Act
        await f.ViewModel.ActivateAsync();

        // Assert
        var query = Assert.Single(f.Orders.Queries);
        Assert.Equal("createdAt:desc", query.Sort);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Null(query.Status);
        Assert.Null(query.Query);
        Assert.Equal(PageContentState.Content, f.ViewModel.ContentState);
        Assert.Equal(3, f.ViewModel.Rows.Count);
        Assert.Equal(45, f.ViewModel.TotalCount);
        Assert.Equal(3, f.ViewModel.TotalPages);
        Assert.Equal("45 đơn · trang 1/3", f.ViewModel.ResultSummary);
        Assert.True(f.ViewModel.ShowPagination);
        Assert.True(f.ViewModel.HasNextPage);
        Assert.False(f.ViewModel.HasPreviousPage);
    }

    [Fact]
    public async Task Rows_AreDisplayReady()
    {
        // Arrange
        var summary = Orders.Summary(code: "ORD-202610-0007", status: OrderStatus.Processing, total: 15_500_000m, customer: "Nguyễn Văn A");
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(new OrderPage([summary], 1, q.PageSize, 1, 1)) });

        // Act
        await f.ViewModel.ActivateAsync();

        // Assert
        var row = Assert.Single(f.ViewModel.Rows);
        Assert.Equal("ORD-202610-0007", row.Code);
        Assert.Equal("Nguyễn Văn A", row.Customer);
        Assert.Equal("Đang xử lý", row.StatusLabel);
        Assert.Equal(StatusTone.Info, row.StatusTone);
        Assert.Equal("Tiền mặt", row.Payment);
        Assert.Equal("15.500.000 ₫", row.Total);
        Assert.Equal("1 dòng", row.ItemCount);
        Assert.Equal(["Hoàn tất"], row.Actions.Select(a => a.Label));
    }

    [Fact]
    public async Task WalkInOrdersShowTheWalkInCustomer()
    {
        // Arrange
        var f = Create();

        // Act
        await f.ViewModel.ActivateAsync();

        // Assert
        Assert.Equal("Khách lẻ", f.ViewModel.Rows.Single().Customer);
    }

    [Fact]
    public async Task EmptyResult_ShowsTheEmptyStateNotAnError()
    {
        // Arrange
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(Orders.Page(q, count: 0)) });

        // Act
        await f.ViewModel.ActivateAsync();

        // Assert
        Assert.Equal(PageContentState.Empty, f.ViewModel.ContentState);
        Assert.True(f.ViewModel.ShowEmptyState);
        Assert.False(f.ViewModel.IsError);
        Assert.False(f.ViewModel.ShowPagination);
    }

    [Fact]
    public async Task ApiFailure_ShowsAnErrorStateAndRetryReloads()
    {
        // Arrange
        var fail = true;
        var f = Create(new FakeOrderRepository
        {
            OnSearch = (q, _) => fail
                ? Task.FromException<OrderPage>(new ApiException(ApiErrorKind.Network, "down"))
                : Task.FromResult(Orders.Page(q))
        });

        // Act
        await f.ViewModel.ActivateAsync();

        // Assert
        Assert.Equal(PageContentState.Error, f.ViewModel.ContentState);
        Assert.Contains("Orders API", f.ViewModel.ErrorMessage);
        Assert.False(f.ViewModel.ShowEmptyState);

        fail = false;
        await f.ViewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal(PageContentState.Content, f.ViewModel.ContentState);
    }

    // ---------- criteria ----------

    [Fact]
    public async Task KeywordIsNotAppliedUntilSubmit_ThenStartsAtPageOneTrimmed()
    {
        // Arrange
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(Orders.Page(q, count: 1, total: 60, totalPages: 3)) });
        await f.ViewModel.ActivateAsync();
        await f.ViewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, f.Orders.Queries.Last().Page);

        // Act
        f.ViewModel.SearchText = "  ORD-2026  ";
        Assert.Equal(2, f.Orders.SearchCount);
        await f.ViewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var query = f.Orders.Queries.Last();
        Assert.Equal("ORD-2026", query.Query);
        Assert.Equal(1, query.Page);
        Assert.Equal(3, f.Orders.SearchCount);
    }

    [Fact]
    public async Task BlankKeywordIsDroppedFromTheQuery()
    {
        // Arrange
        var f = Create();
        await f.ViewModel.ActivateAsync();
        f.ViewModel.SearchText = "   ";

        // Act
        await f.ViewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(f.Orders.Queries.Last().Query);
    }

    [Fact]
    public async Task SelectingStatusSortOrPageSize_AppliesAtOnceFromPageOne_KeepingTheAppliedKeyword()
    {
        // Arrange
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(Orders.Page(q, count: 1, total: 100, totalPages: 5)) });
        await f.ViewModel.ActivateAsync();
        f.ViewModel.SearchText = "applied";
        await f.ViewModel.SearchCommand.ExecuteAsync(null);
        await f.ViewModel.NextPageCommand.ExecuteAsync(null);
        f.ViewModel.SearchText = "typed but not submitted";

        // Act
        f.ViewModel.SelectedStatusFilter = f.ViewModel.StatusFilters.Single(o => o.Value == OrderStatus.Completed);
        var afterStatus = f.Orders.Queries.Last();
        f.ViewModel.SelectedSort = f.ViewModel.SortOptions.Single(o => o.Value == "totalAmount:asc");
        var afterSort = f.Orders.Queries.Last();
        f.ViewModel.SelectedPageSize = 50;
        var afterSize = f.Orders.Queries.Last();

        // Assert
        Assert.Equal(OrderStatus.Completed, afterStatus.Status);
        Assert.Equal(1, afterStatus.Page);
        Assert.Equal("applied", afterStatus.Query);
        Assert.Equal("totalAmount:asc", afterSort.Sort);
        Assert.Equal(OrderStatus.Completed, afterSort.Status);
        Assert.Equal(50, afterSize.PageSize);
        Assert.Equal("totalAmount:asc", afterSize.Sort);
        Assert.Equal("applied", afterSize.Query);
    }

    [Fact]
    public async Task ResetFilters_SubmitsTheDefaultsOnceAtPageOne()
    {
        // Arrange
        var f = Create();
        await f.ViewModel.ActivateAsync();
        f.ViewModel.SearchText = "abc";
        f.ViewModel.SelectedStatusFilter = f.ViewModel.StatusFilters.Single(o => o.Value == OrderStatus.Cancelled);
        f.ViewModel.SelectedPageSize = 100;
        var before = f.Orders.SearchCount;

        // Act
        await f.ViewModel.ResetFiltersCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(before + 1, f.Orders.SearchCount);
        var query = f.Orders.Queries.Last();
        Assert.Null(query.Status);
        Assert.Null(query.Query);
        Assert.Equal("createdAt:desc", query.Sort);
        Assert.Equal(20, query.PageSize);
        Assert.Equal(1, query.Page);
        Assert.Equal(string.Empty, f.ViewModel.SearchText);
    }

    [Fact]
    public async Task ClearSearch_EmptiesTheBoxAndReloads()
    {
        // Arrange
        var f = Create();
        await f.ViewModel.ActivateAsync();
        f.ViewModel.SearchText = "abc";
        await f.ViewModel.SearchCommand.ExecuteAsync(null);

        // Act
        await f.ViewModel.ClearSearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(f.Orders.Queries.Last().Query);
        Assert.Equal(string.Empty, f.ViewModel.SearchText);
    }

    // ---------- paging ----------

    [Fact]
    public async Task Paging_MovesWithinBoundsUsingServerMetadata()
    {
        // Arrange
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(Orders.Page(q, count: 1, total: 41, totalPages: 3)) });
        await f.ViewModel.ActivateAsync();

        // Act / Assert
        Assert.False(f.ViewModel.PreviousPageCommand.CanExecute(null));
        await f.ViewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, f.ViewModel.CurrentPage);
        await f.ViewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(3, f.ViewModel.CurrentPage);
        Assert.False(f.ViewModel.NextPageCommand.CanExecute(null));
        await f.ViewModel.PreviousPageCommand.ExecuteAsync(null);
        Assert.Equal(2, f.ViewModel.CurrentPage);
        Assert.Equal([1, 2, 3, 2], f.Orders.Queries.Select(q => q.Page));
    }

    [Fact]
    public async Task PageBeyondTheEnd_FallsBackToPageOneExactlyOnce()
    {
        // Arrange: page 2 vanished because the last order on it was cancelled.
        var f = Create(new FakeOrderRepository
        {
            OnSearch = (q, _) => Task.FromResult(q.Page == 1
                ? Orders.Page(q, count: 2, total: 21, totalPages: 2)
                : new OrderPage([], q.Page, q.PageSize, 20, 1))
        });
        await f.ViewModel.ActivateAsync();
        await f.ViewModel.NextPageCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([1, 2, 1], f.Orders.Queries.Select(q => q.Page));
        Assert.Equal(PageContentState.Content, f.ViewModel.ContentState);
        Assert.Equal(1, f.ViewModel.CurrentPage);
        Assert.Equal(2, f.ViewModel.Rows.Count);
    }

    [Fact]
    public async Task NewerSearchReplacesAnOlderOneStillInFlight()
    {
        // Arrange
        var pending = new TaskCompletionSource<OrderPage>();
        var calls = 0;
        var f = Create(new FakeOrderRepository
        {
            OnSearch = (q, token) => ++calls == 2
                ? pending.Task
                : Task.FromResult(Orders.Page(q, count: calls == 1 ? 1 : 4))
        });
        await f.ViewModel.ActivateAsync();

        // Act: second search stays pending, third completes first.
        f.ViewModel.SearchText = "slow";
        var slow = f.ViewModel.SearchCommand.ExecuteAsync(null);
        f.ViewModel.SearchText = "fast";
        await f.ViewModel.SearchCommand.ExecuteAsync(null);
        pending.SetResult(Orders.Page(new OrderQuery(), count: 9));
        await slow;

        // Assert
        Assert.Equal(4, f.ViewModel.Rows.Count);
    }

    // ---------- navigation ----------

    [Fact]
    public async Task NewOrder_NavigatesToTheCreateRoute()
    {
        // Arrange
        var f = Create();
        await f.ViewModel.ActivateAsync();

        // Act
        f.ViewModel.NewOrderCommand.Execute(null);

        // Assert
        Assert.Equal(AppRoute.OrderCreate(), Assert.Single(f.Navigation.Routes));
    }

    // ---------- status actions ----------

    private static async Task<(Fixture F, OrderRowAction Pay, OrderRowAction Complete, OrderRowAction Cancel)> PendingOrder(
        Action<FakeOrderRepository>? configure = null)
    {
        var summary = Orders.Summary(Orders.FirstId, "ORD-202610-0001", OrderStatus.Pending);
        var repository = new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(new OrderPage([summary], 1, q.PageSize, 1, 1)) };
        configure?.Invoke(repository);
        var f = Create(repository);
        await f.ViewModel.ActivateAsync();
        var actions = f.ViewModel.Rows.Single().Actions;
        return (f, actions[0], actions[1], actions[2]);
    }

    [Fact]
    public async Task PendingRow_OffersPayCompleteAndCancel()
    {
        // Arrange
        var (_, pay, complete, cancel) = await PendingOrder();

        // Assert
        Assert.Equal(OrderStatus.Processing, pay.Action.Target);
        Assert.Equal(OrderStatus.Completed, complete.Action.Target);
        Assert.Equal(OrderStatus.Cancelled, cancel.Action.Target);
        Assert.True(cancel.IsDestructive);
    }

    [Fact]
    public async Task ConfirmedAction_PatchesTheTargetStatus_ReloadsAndReportsSuccess()
    {
        // Arrange
        var (f, _, complete, _) = await PendingOrder();
        var searchesBefore = f.Orders.SearchCount;

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(complete);

        // Assert
        Assert.Equal((Orders.FirstId, OrderStatus.Completed), Assert.Single(f.Orders.Updates));
        Assert.Equal(searchesBefore + 1, f.Orders.SearchCount);
        Assert.Contains("ORD-202610-0001", f.ViewModel.ActionMessage);
        Assert.Contains("Hoàn tất", f.ViewModel.ActionMessage);
        Assert.False(f.ViewModel.IsBusy);
        var request = Assert.Single(f.Dialogs.Requests);
        Assert.Contains("ORD-202610-0001", request.Message);
    }

    [Theory]
    [InlineData(0, "Xác nhận thanh toán")]
    [InlineData(1, "Hoàn tất đơn")]
    [InlineData(2, "Hủy đơn")]
    public async Task EveryActionAsksForConfirmationFirst(int index, string title)
    {
        // Arrange
        var (f, pay, complete, cancel) = await PendingOrder();
        var action = new[] { pay, complete, cancel }[index];

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(action);

        // Assert
        Assert.Equal(title, Assert.Single(f.Dialogs.Requests).Title);
    }

    [Theory]
    [InlineData(DialogResult.Cancelled)]
    [InlineData(DialogResult.Dismissed)]
    public async Task DeclinedConfirmation_SendsNothing(DialogResult answer)
    {
        // Arrange
        var (f, _, _, cancel) = await PendingOrder();
        f.Dialogs.Result = answer;
        var searchesBefore = f.Orders.SearchCount;

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(cancel);

        // Assert
        Assert.Empty(f.Orders.Updates);
        Assert.Equal(searchesBefore, f.Orders.SearchCount);
        Assert.False(f.ViewModel.HasActionMessage);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Conflict_ShowsTheServerReasonAndReloadsOnce()
    {
        // Arrange
        var (f, _, complete, _) = await PendingOrder(r =>
            r.OnUpdate = (_, _, _) => Task.FromException<OrderDetail>(Orders.Http(409, "Device 358912345678902 is no longer available.")));
        var searchesBefore = f.Orders.SearchCount;

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(complete);

        // Assert
        Assert.Equal("Device 358912345678902 is no longer available.", f.ViewModel.ActionMessage);
        Assert.Single(f.Orders.Updates);
        Assert.Equal(searchesBefore + 1, f.Orders.SearchCount);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task NotFound_ExplainsAndReloads()
    {
        // Arrange
        var (f, pay, _, _) = await PendingOrder(r =>
            r.OnUpdate = (_, _, _) => Task.FromException<OrderDetail>(Orders.Http(404)));
        var searchesBefore = f.Orders.SearchCount;

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(pay);

        // Assert
        Assert.Equal("Đơn ORD-202610-0001 không còn tồn tại.", f.ViewModel.ActionMessage);
        Assert.Equal(searchesBefore + 1, f.Orders.SearchCount);
    }

    [Fact]
    public async Task NetworkFailure_KeepsTheListAndDoesNotRetry()
    {
        // Arrange
        var (f, _, complete, _) = await PendingOrder(r =>
            r.OnUpdate = (_, _, _) => Task.FromException<OrderDetail>(new ApiException(ApiErrorKind.Network, "down")));
        var searchesBefore = f.Orders.SearchCount;

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(complete);

        // Assert
        Assert.Single(f.Orders.Updates);
        Assert.Equal(searchesBefore, f.Orders.SearchCount);
        Assert.Contains("kết nối", f.ViewModel.ActionMessage);
        Assert.Equal(PageContentState.Content, f.ViewModel.ContentState);
    }

    [Fact]
    public async Task UnavailableDialogHost_ReportsInsteadOfCrashing()
    {
        // Arrange
        var (f, _, complete, _) = await PendingOrder();
        f.Dialogs.Throw = new DialogUnavailableException("not ready");

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(complete);

        // Assert
        Assert.Empty(f.Orders.Updates);
        Assert.True(f.ViewModel.HasActionMessage);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task ChangeStatus_IgnoresANullAction()
    {
        // Arrange
        var (f, _, _, _) = await PendingOrder();

        // Act
        await f.ViewModel.ChangeStatusCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(f.Dialogs.Requests);
        Assert.Empty(f.Orders.Updates);
    }

    [Fact]
    public async Task CompletedRow_OffersNoActions()
    {
        // Arrange
        var summary = Orders.Summary(status: OrderStatus.Completed);
        var f = Create(new FakeOrderRepository { OnSearch = (q, _) => Task.FromResult(new OrderPage([summary], 1, q.PageSize, 1, 1)) });

        // Act
        await f.ViewModel.ActivateAsync();

        // Assert
        Assert.False(f.ViewModel.Rows.Single().HasActions);
    }

    // ---------- detail ----------

    [Fact]
    public async Task OpenDetail_LoadsTheOrderThenPresentsIt()
    {
        // Arrange
        var f = Create();
        await f.ViewModel.ActivateAsync();
        var row = f.ViewModel.Rows.Single();

        // Act
        await f.ViewModel.OpenDetailCommand.ExecuteAsync(row);

        // Assert
        Assert.Equal(Orders.FirstId, Assert.Single(f.Orders.Fetched));
        var shown = Assert.Single(f.Presenter.Shown);
        Assert.Equal(Orders.FirstId, shown.Id);
        Assert.False(f.ViewModel.HasActionMessage);
    }

    [Fact]
    public async Task OpenDetail_WhenTheOrderIsGone_ReportsAndReloadsWithoutOpeningAnything()
    {
        // Arrange
        var f = Create(new FakeOrderRepository { OnGet = (_, _) => Task.FromResult<OrderDetail?>(null) });
        await f.ViewModel.ActivateAsync();
        var searchesBefore = f.Orders.SearchCount;

        // Act
        await f.ViewModel.OpenDetailCommand.ExecuteAsync(f.ViewModel.Rows.Single());

        // Assert
        Assert.Empty(f.Presenter.Shown);
        Assert.Contains("không còn tồn tại", f.ViewModel.ActionMessage);
        Assert.Equal(searchesBefore + 1, f.Orders.SearchCount);
    }

    [Fact]
    public async Task OpenDetail_WhenLoadingFails_ReportsAndOpensNothing()
    {
        // Arrange
        var f = Create(new FakeOrderRepository
        {
            OnGet = (_, _) => Task.FromException<OrderDetail?>(new ApiException(ApiErrorKind.Timeout, "slow"))
        });
        await f.ViewModel.ActivateAsync();

        // Act
        await f.ViewModel.OpenDetailCommand.ExecuteAsync(f.ViewModel.Rows.Single());

        // Assert
        Assert.Empty(f.Presenter.Shown);
        Assert.Contains("quá thời gian", f.ViewModel.ActionMessage);
    }
}
