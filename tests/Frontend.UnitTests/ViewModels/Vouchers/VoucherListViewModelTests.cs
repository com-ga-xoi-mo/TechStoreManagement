using TechStore.Client.Models.Vouchers;
using TechStore.Client.Repositories.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Vouchers;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Vouchers;

public sealed class VoucherListViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Activate_LoadsFirstPageWithRowsAndCount()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10"), Voucher("TET2027")], TotalCount = 2 };
        var viewModel = Create(repository);

        // Act
        await viewModel.ActivateAsync();

        // Assert
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.Equal(["SALE10", "TET2027"], viewModel.Rows.Select(row => row.Code));
        Assert.Equal("2 voucher", viewModel.CountLabel);
        Assert.Null(repository.Queries.Single().IsActive);
        Assert.Equal(1, repository.Queries.Single().Page);
        Assert.False(viewModel.ShowEmptyState);
        Assert.True(viewModel.ShowPagination);
    }

    [Fact]
    public async Task Activate_NoVouchers_ShowsEmptyState()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());

        // Act
        await viewModel.ActivateAsync();

        // Assert
        Assert.Equal(PageContentState.Empty, viewModel.ContentState);
        Assert.True(viewModel.ShowEmptyState);
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task Activate_RowsUseTheInjectedClockForStatus()
    {
        // Arrange
        var expired = Voucher("OLD", expiresAt: Now.UtcDateTime.AddDays(-1));
        var repository = new FakeVouchers { Items = [expired], TotalCount = 1 };
        var viewModel = Create(repository);

        // Act
        await viewModel.ActivateAsync();

        // Assert
        Assert.Equal("Hết hạn", viewModel.Rows.Single().StatusLabel);
    }

    [Fact]
    public async Task Activate_NetworkFailure_ShowsSafeErrorAndRetryRecovers()
    {
        // Arrange
        var repository = new FakeVouchers { SearchException = new ApiException(ApiErrorKind.Network, "private details") };
        var viewModel = Create(repository);

        // Act
        await viewModel.ActivateAsync();

        // Assert
        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Contains("kết nối", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.RetryCommand.CanExecute(null));

        repository.SearchException = null;
        repository.Items = [Voucher("SALE10")];
        repository.TotalCount = 1;
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Theory]
    [InlineData(401, "hết hạn")]
    [InlineData(403, "không có quyền")]
    public async Task Activate_HttpFailure_ExplainsInVietnamese(int status, string fragment)
    {
        // Arrange
        var repository = new FakeVouchers { SearchException = new ApiException(ApiErrorKind.Http, "x", status) };
        var viewModel = Create(repository);

        // Act
        await viewModel.ActivateAsync();

        // Assert
        Assert.Contains(fragment, viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedSearch_DoesNotLeaveStaleRows()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        repository.SearchException = new ApiException(ApiErrorKind.Network, "down");

        // Act
        viewModel.SearchText = "x";
        await viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Empty(viewModel.Rows);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task StatusFilter_MapsToTheIsActiveQuery(int index, bool? expected)
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        // Act
        viewModel.SelectedStatusIndex = index;
        await viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(expected, repository.Queries.Last().IsActive);
        Assert.Equal(1, repository.Queries.Last().Page);
    }

    [Fact]
    public async Task Search_TrimsTextAndResetsToFirstPage()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 45, TotalPages = 3, PageSize = 20 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        await viewModel.NextPageCommand.ExecuteAsync(null);

        // Act
        viewModel.SearchText = "  sale ";
        await viewModel.SearchCommand.ExecuteAsync(null);

        // Assert
        var query = repository.Queries.Last();
        Assert.Equal("sale", query.Query);
        Assert.Equal(1, query.Page);
    }

    [Fact]
    public async Task ClearSearch_ResetsTextAndStatus()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        viewModel.SearchText = "sale";
        viewModel.SelectedStatusIndex = 2;

        // Act
        await viewModel.ClearSearchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(0, viewModel.SelectedStatusIndex);
        Assert.Null(repository.Queries.Last().Query);
        Assert.Null(repository.Queries.Last().IsActive);
    }

    [Fact]
    public async Task Paging_UsesServerMetadata()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 45, TotalPages = 3, PageSize = 20 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        // Assert first page
        Assert.False(viewModel.HasPreviousPage);
        Assert.True(viewModel.HasNextPage);
        Assert.Equal("Trang 1 / 3", viewModel.PageLabel);
        Assert.Equal("Hiển thị 1–20 trong 45 voucher", viewModel.RangeLabel);

        // Act
        await viewModel.NextPageCommand.ExecuteAsync(null);

        // Assert second page
        Assert.Equal(2, repository.Queries.Last().Page);
        Assert.Equal("Trang 2 / 3", viewModel.PageLabel);
        Assert.True(viewModel.HasPreviousPage);

        await viewModel.PreviousPageCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.Queries.Last().Page);
    }

    [Fact]
    public async Task ToggleActive_Deactivate_UpdatesTheRowInPlace()
    {
        // Arrange
        var voucher = Voucher("SALE10");
        var repository = new FakeVouchers { Items = [voucher, Voucher("TET2027")], TotalCount = 2 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        var row = viewModel.Rows[0];

        // Act
        await viewModel.ToggleActiveCommand.ExecuteAsync(row);

        // Assert
        Assert.Equal((voucher.Id, false), repository.SetActiveCalls.Single());
        Assert.Equal("Đã tắt", viewModel.Rows[0].StatusLabel);
        Assert.True(viewModel.Rows[0].CanActivate);
        Assert.Equal("Đang hoạt động", viewModel.Rows[1].StatusLabel);
        Assert.Equal(string.Empty, viewModel.ActionErrorMessage);
    }

    [Fact]
    public async Task ToggleActive_Activate_CallsActivate()
    {
        // Arrange
        var voucher = Voucher("SALE10", isActive: false);
        var repository = new FakeVouchers { Items = [voucher], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        // Act
        await viewModel.ToggleActiveCommand.ExecuteAsync(viewModel.Rows[0]);

        // Assert
        Assert.Equal((voucher.Id, true), repository.SetActiveCalls.Single());
        Assert.Equal("Đang hoạt động", viewModel.Rows[0].StatusLabel);
    }

    [Fact]
    public async Task ToggleActive_Forbidden_KeepsTheRowAndShowsMessage()
    {
        // Arrange
        var repository = new FakeVouchers
        {
            Items = [Voucher("SALE10")],
            TotalCount = 1,
            SetActiveException = new ApiException(ApiErrorKind.Http, "x", 403)
        };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        // Act
        await viewModel.ToggleActiveCommand.ExecuteAsync(viewModel.Rows[0]);

        // Assert
        Assert.Equal("Đang hoạt động", viewModel.Rows[0].StatusLabel);
        Assert.Contains("không có quyền", viewModel.ActionErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.HasActionError);
    }

    [Fact]
    public async Task ToggleActive_NotFound_ReloadsTheList()
    {
        // Arrange
        var repository = new FakeVouchers
        {
            Items = [Voucher("SALE10")],
            TotalCount = 1,
            SetActiveException = new ApiException(ApiErrorKind.Http, "x", 404)
        };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        // Act
        await viewModel.ToggleActiveCommand.ExecuteAsync(viewModel.Rows[0]);

        // Assert
        Assert.Equal(2, repository.Queries.Count);
        Assert.Contains("không còn", viewModel.ActionErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToggleActive_IgnoresUnexpectedParameters()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        // Act
        await viewModel.ToggleActiveCommand.ExecuteAsync(null);
        await viewModel.ToggleActiveCommand.ExecuteAsync(viewModel);

        // Assert
        Assert.Empty(repository.SetActiveCalls);
    }

    [Fact]
    public async Task OpenCreate_RequestsTheEditorWithoutAVoucher()
    {
        // Arrange
        var viewModel = Create(new FakeVouchers());
        var requested = new List<VoucherItem?>();
        viewModel.VoucherEditorRequested += requested.Add;

        // Act
        viewModel.OpenCreateCommand.Execute(null);

        // Assert
        Assert.Equal([null], requested);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task OpenEdit_RequestsTheEditorWithTheRowVoucher_AndIgnoresOtherParameters()
    {
        // Arrange
        var voucher = Voucher("SALE10");
        var repository = new FakeVouchers { Items = [voucher], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        var requested = new List<VoucherItem?>();
        viewModel.VoucherEditorRequested += requested.Add;

        // Act
        viewModel.OpenEditCommand.Execute(viewModel.Rows[0]);
        viewModel.OpenEditCommand.Execute(null);
        viewModel.OpenEditCommand.Execute("not a row");

        // Assert
        Assert.Same(voucher, Assert.Single(requested));
    }

    [Fact]
    public async Task NotifySaved_ReloadsTheCurrentCriteria()
    {
        // Arrange
        var repository = new FakeVouchers { Items = [Voucher("SALE10")], TotalCount = 1 };
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        repository.Items = [Voucher("SALE10"), Voucher("NEW20")];
        repository.TotalCount = 2;

        // Act
        await viewModel.NotifySavedAsync();

        // Assert
        Assert.Equal(["SALE10", "NEW20"], viewModel.Rows.Select(row => row.Code));
        Assert.Equal(2, repository.Queries.Count);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("SalesStaff", false)]
    public void CanManageVouchers_FollowsTheSessionRole(string role, bool expected)
    {
        // Arrange
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "user", "User", role);
        var viewModel = Create(new FakeVouchers(), session: session);

        // Act and Assert
        Assert.Equal(expected, viewModel.CanManageVouchers);
    }

    [Fact]
    public void Back_NavigatesToTheCustomersList()
    {
        // Arrange
        var navigation = new RecordingNavigation();
        var viewModel = Create(new FakeVouchers(), navigation: navigation);

        // Act
        viewModel.BackCommand.Execute(null);

        // Assert
        Assert.Equal(AppRoute.Root(ShellDestination.Customers), navigation.Navigated.Single());
    }

    [Fact]
    public async Task Deactivate_CancelsAnInFlightSearch()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var repository = new FakeVouchers { Gate = gate.Task };
        var viewModel = Create(repository);
        var activation = viewModel.ActivateAsync();

        // Act
        viewModel.Deactivate();
        gate.SetResult();
        await activation;

        // Assert
        Assert.NotEqual(PageContentState.Content, viewModel.ContentState);
        Assert.Empty(viewModel.Rows);
    }

    private static VoucherListViewModel Create(
        FakeVouchers repository,
        ISessionStore? session = null,
        INavigationService? navigation = null) =>
        new(repository, navigation ?? new RecordingNavigation(), session, new FixedClock(Now));

    private static VoucherItem Voucher(string code, bool isActive = true, DateTime? expiresAt = null) => new(
        Guid.NewGuid(), code, "Giảm giá thử", "Percentage", 10m, 0m, null, 50, 0, 0,
        expiresAt ?? Now.UtcDateTime.AddDays(30), isActive, Now.UtcDateTime.AddDays(-10));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingNavigation : INavigationService
    {
        public List<AppRoute> Navigated { get; } = [];
        public AppRoute CurrentRoute => AppRoute.Root(ShellDestination.Customers);
        public ShellDestination CurrentDestination => ShellDestination.Customers;
        public event EventHandler<ShellDestination>? DestinationChanged { add { } remove { } }
        public event EventHandler<AppRoute>? RouteChanged { add { } remove { } }
        public void Reset(AppRoute route) { }
        public NavigationOutcome Navigate(ShellDestination destination) { Navigated.Add(AppRoute.Root(destination)); return NavigationOutcome.Navigated; }
        public NavigationOutcome Navigate(AppRoute route) { Navigated.Add(route); return NavigationOutcome.Navigated; }
    }

    private sealed class FakeVouchers : IVoucherRepository
    {
        public IReadOnlyList<VoucherItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Exception? SearchException { get; set; }
        public Exception? SetActiveException { get; set; }
        public Task? Gate { get; set; }
        public List<VoucherQuery> Queries { get; } = [];
        public List<(Guid Id, bool IsActive)> SetActiveCalls { get; } = [];

        public async Task<VoucherPage> SearchAsync(VoucherQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Gate is not null) await Gate.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (SearchException is not null) throw SearchException;
            return new(TotalCount == 0 ? [] : Items, query.Page, PageSize, TotalCount, TotalCount == 0 ? 0 : TotalPages);
        }

        public Task<VoucherItem> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<VoucherItem> UpdateAsync(Guid voucherId, UpdateVoucherRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<VoucherItem> SetActiveAsync(Guid voucherId, bool isActive, CancellationToken cancellationToken = default)
        {
            SetActiveCalls.Add((voucherId, isActive));
            if (SetActiveException is not null) return Task.FromException<VoucherItem>(SetActiveException);
            return Task.FromResult(Items.Single(item => item.Id == voucherId) with { IsActive = isActive });
        }
    }
}
