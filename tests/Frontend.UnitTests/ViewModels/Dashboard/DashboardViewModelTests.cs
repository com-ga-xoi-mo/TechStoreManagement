using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.ViewModels.Dashboard;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task ActivateAsync_LoadsDefaultPeriodAndCommitsOneConsistentSnapshot()
    {
        var repository = new AnalyticsRepositoryFake(new FixtureAnalyticsRepository());
        var viewModel = Create(repository);

        await viewModel.ActivateAsync();

        Assert.Equal(DashboardPeriod.Last7Days, viewModel.SelectedPeriod);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.True(viewModel.IsDemoMode);
        Assert.True(viewModel.CanChangePeriod);
        Assert.NotNull(viewModel.Snapshot);
        Assert.Equal(viewModel.Snapshot!.Summary.Revenue, viewModel.Snapshot.DailyRevenue.Sum(point => point.Revenue));
    }

    [Fact]
    public async Task ChartPeakSummary_UsesSnapshotPeakAndAsOfMetadata()
    {
        var viewModel = Create(new RecordingAnalyticsRepository(), demoEnabled: false);

        await viewModel.ActivateAsync();

        Assert.Contains("Đỉnh", viewModel.ChartPeakSummary);
        Assert.Contains("99", viewModel.ChartPeakSummary);
        Assert.Contains("₫", viewModel.ChartPeakSummary);
        Assert.Contains("UTC+07", viewModel.ChartPeakSummary);
    }

    [Fact]
    public async Task SelectPeriodAsync_UsesRepositoryAndUnavailableDoesNotBecomeZeroContent()
    {
        var repository = new AnalyticsRepositoryFake(new UnavailableAnalyticsRepository());
        var viewModel = Create(repository, demoEnabled: false);
        await viewModel.ActivateAsync();

        await viewModel.SelectPeriodCommand.ExecuteAsync(0);

        Assert.Equal(DashboardPeriod.Today, viewModel.SelectedPeriod);
        Assert.Equal(PageContentState.Empty, viewModel.ContentState);
        Assert.True(viewModel.IsUnavailable);
        Assert.True(viewModel.CanChangePeriod);
        Assert.Null(viewModel.Snapshot);
        Assert.Equal("—", viewModel.LowStockText);
        Assert.Empty(viewModel.LowStockTrendText);
        Assert.Equal("Số balance có Quantity ≤ MinStockAlert", viewModel.LowStockDetailText);
        Assert.Equal(2, repository.Queries.Count);
    }

    [Fact]
    public async Task SelectPeriodAsync_LatestRequestOwnsSnapshotAndState()
    {
        var firstCompletion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new SequencedAnalyticsRepository(firstCompletion, secondCompletion);
        var session = new InMemorySessionStore();
        session.SetSession("token-a", "refresh-a", "admin", "Admin A", "Admin");
        var viewModel = Create(repository, demoEnabled: false, sessionStore: session);
        var initial = viewModel.ActivateAsync();
        Assert.Equal(DashboardPeriod.Last7Days, repository.Queries[0].Period);

        session.SetSession("token-b", "refresh-b", "admin", "Admin B", "Admin");
        Assert.Equal(2, repository.Queries.Count);
        secondCompletion.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Today, 222m)));
        await WaitUntilAsync(() => viewModel.Snapshot?.Summary.Revenue == 222m);

        firstCompletion.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Last7Days, 111m)));
        await initial;

        Assert.Equal(222m, viewModel.Snapshot!.Summary.Revenue);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task SessionReplacement_LateErrorCannotReplaceCurrentSessionState()
    {
        var firstCompletion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new InMemorySessionStore();
        session.SetSession("token-a", "refresh-a", "admin", "Admin A", "Admin");
        var viewModel = Create(new SequencedAnalyticsRepository(firstCompletion, secondCompletion),
            demoEnabled: false, sessionStore: session);
        var initial = viewModel.ActivateAsync();

        session.SetSession("token-b", "refresh-b", "admin", "Admin B", "Admin");
        secondCompletion.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Last7Days, 222m, DashboardSourceKind.Live)));
        await WaitUntilAsync(() => viewModel.Snapshot?.Summary.Revenue == 222m);
        firstCompletion.SetException(new ApiException(ApiErrorKind.Http, "late failure", 500));
        await initial;

        Assert.Equal(222m, viewModel.Snapshot!.Summary.Revenue);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.Empty(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SelectPeriodAsync_FailureCanRetryTheRequestedPeriod()
    {
        var repository = new FailOnceAnalyticsRepository();
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();

        await viewModel.SelectPeriodCommand.ExecuteAsync(0);
        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Equal(DashboardPeriod.Today, viewModel.SelectedPeriod);

        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.Equal(DashboardPeriod.Today, repository.LastQuery!.Period);
        Assert.Equal(1_234m, viewModel.Snapshot!.Summary.Revenue);
    }

    [Fact]
    public async Task LiveDashboard_AllowsPeriodSelectionAndRefreshForCurrentPeriod()
    {
        var repository = new RecordingAnalyticsRepository();
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var viewModel = Create(repository, demoEnabled: false, sessionStore: session);

        await viewModel.ActivateAsync();
        await viewModel.SelectPeriodCommand.ExecuteAsync(0);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(new[] { DashboardPeriod.Last7Days, DashboardPeriod.Today, DashboardPeriod.Today },
            repository.Queries.Select(query => query.Period));
        Assert.False(viewModel.IsDemoMode);
        Assert.True(viewModel.CanChangePeriod);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task CustomRange_DraftAndInvalidInputDoNotLoad_ApplyAndRefreshUseCommittedDates()
    {
        var repository = new RecordingAnalyticsRepository();
        var viewModel = Create(repository, demoEnabled: false);
        await viewModel.ActivateAsync();
        viewModel.ToggleCustomRangeCommand.Execute(null);
        Assert.True(viewModel.ShowCustomRange);

        viewModel.CustomFrom = "2024-10-12";
        viewModel.CustomTo = "18/10/2024";
        await viewModel.ApplyCustomRangeCommand.ExecuteAsync(null);
        Assert.NotEmpty(viewModel.CustomFromError);
        Assert.Single(repository.Queries);
        Assert.Equal(DashboardPeriod.Last7Days, viewModel.SelectedPeriod);

        viewModel.CustomFrom = "12/10/2024";
        Assert.Single(repository.Queries);
        await viewModel.ApplyCustomRangeCommand.ExecuteAsync(null);
        var custom = new DashboardQuery(DashboardPeriod.Custom, (new(2024, 10, 12), new(2024, 10, 18)));
        Assert.Equal(custom, repository.Queries[^1]);
        Assert.Equal(DashboardPeriod.Custom, viewModel.SelectedPeriod);
        Assert.Contains("12/10/2024", viewModel.ReferenceLabel);

        viewModel.CustomTo = "20/10/2024";
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(custom, repository.Queries[^1]);
        await viewModel.SelectPeriodCommand.ExecuteAsync(0);
        Assert.Equal(new DashboardQuery(DashboardPeriod.Today), repository.Queries[^1]);
        Assert.False(viewModel.ShowCustomRange);
    }

    [Theory]
    [InlineData("12/10/2024", "11/10/2024")]
    [InlineData("01/10/2024", "01/11/2024")]
    [InlineData("12/10/2024", "31/12/9999")]
    public async Task CustomRange_WhenEndDateInvalid_ShowsFieldError(string from, string to)
    {
        var repository = new RecordingAnalyticsRepository();
        var viewModel = Create(repository, demoEnabled: false);
        await viewModel.ActivateAsync();
        viewModel.CustomFrom = from;
        viewModel.CustomTo = to;

        await viewModel.ApplyCustomRangeCommand.ExecuteAsync(null);

        Assert.NotEmpty(viewModel.CustomToError);
        Assert.Single(repository.Queries);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task CustomRange_FailedRequestRetriesAppliedDates()
    {
        var repository = new FailOnceAnalyticsRepository();
        var viewModel = Create(repository, demoEnabled: false);
        await viewModel.ActivateAsync();
        viewModel.CustomFrom = "12/10/2024";
        viewModel.CustomTo = "18/10/2024";
        await viewModel.ApplyCustomRangeCommand.ExecuteAsync(null);
        Assert.Equal(PageContentState.Error, viewModel.ContentState);

        viewModel.CustomFrom = "13/10/2024";
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(new DashboardQuery(DashboardPeriod.Custom, (new(2024, 10, 12), new(2024, 10, 18))), repository.LastQuery);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task CustomRange_WhenLogoutCancelsInFlightRequest_DropsLateResult()
    {
        var first = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var custom = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Last7Days, 222m, DashboardSourceKind.Live)));
        var repository = new SequencedAnalyticsRepository(first, custom);
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "manager1", "Manager", "Manager");
        var viewModel = Create(repository, demoEnabled: false, sessionStore: session);
        await viewModel.ActivateAsync();
        viewModel.CustomFrom = "12/10/2024";
        viewModel.CustomTo = "18/10/2024";

        var request = viewModel.ApplyCustomRangeCommand.ExecuteAsync(null);
        Assert.Equal(DashboardPeriod.Custom, repository.Queries[^1].Period);
        session.Clear();
        Assert.True(repository.LastToken.IsCancellationRequested);
        custom.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Custom, 999m, DashboardSourceKind.Live)));
        await request;

        Assert.Null(viewModel.Snapshot);
        Assert.True(viewModel.IsSignedOut);
    }

    [Fact]
    public async Task Preview_CustomRangeCommandsDoNotOpenOrRequestCustomData()
    {
        var repository = new AnalyticsRepositoryFake(new FixtureAnalyticsRepository());
        var viewModel = Create(repository);
        await viewModel.ActivateAsync();
        viewModel.ToggleCustomRangeCommand.Execute(null);
        viewModel.CustomFrom = "12/10/2024";
        viewModel.CustomTo = "18/10/2024";

        await viewModel.ApplyCustomRangeCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowCustomRange);
        Assert.Equal(3, viewModel.Periods.Count);
        Assert.Equal(DashboardPeriod.Last7Days, Assert.Single(repository.Queries).Period);
    }

    [Fact]
    public async Task SessionExpired_ClearsSnapshotAndShowsAuthenticationError()
    {
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var viewModel = Create(new AnalyticsRepositoryFake(new FixtureAnalyticsRepository()), demoEnabled: false, sessionStore: session);
        await viewModel.ActivateAsync();

        Assert.True(session.TryExpire(session.Current.Generation));

        Assert.Null(viewModel.Snapshot);
        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Contains("hết hạn", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ClearsLiveSnapshotAndLeavesSignInRequiredEmptyState()
    {
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var viewModel = Create(new AnalyticsRepositoryFake(new FixtureAnalyticsRepository()), demoEnabled: false, sessionStore: session);
        await viewModel.ActivateAsync();

        session.Clear();

        Assert.Null(viewModel.Snapshot);
        Assert.Equal(PageContentState.Empty, viewModel.ContentState);
        Assert.True(viewModel.IsSignedOut);
        Assert.Contains("Đăng nhập", viewModel.EmptyDashboardMessage);
    }

    [Fact]
    public async Task ForbiddenRequest_ShowsAccessDeniedAndPreservesSession()
    {
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var repository = new ThrowingAnalyticsRepository(new ApiException(ApiErrorKind.Http, "Forbidden", 403));
        var viewModel = Create(repository, demoEnabled: false, sessionStore: session);

        await viewModel.ActivateAsync();

        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Contains("không có quyền", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SessionStatus.Active, session.Current.Status);
    }

    [Fact]
    public async Task ActivationLease_CancelsAndWaitsForDashboardRequestWithoutCommittingLateData()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new UncooperativeAnalyticsRepository(started, completion);
        var viewModel = Create(repository);
        var disposeCount = 0;
        var lease = viewModel.CreateActivationLease(() =>
        {
            disposeCount++;
            return ValueTask.CompletedTask;
        });
        lease.Start(token => viewModel.ActivateAsync(token));
        await started.Task;

        var cleanup = lease.Deactivate();
        Assert.False(cleanup.IsCompleted);
        completion.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Last7Days, 999m)));
        await cleanup;

        Assert.Null(viewModel.Snapshot);
        Assert.Equal(PageContentState.Loading, viewModel.ContentState);
        Assert.Equal(1, disposeCount);
        await lease.Deactivate();
        Assert.Equal(1, disposeCount);
    }

    private static DashboardViewModel Create(IAnalyticsRepository repository, bool demoEnabled = true, ISessionStore? sessionStore = null) =>
        new(repository, new DashboardPreviewOptions(demoEnabled), sessionStore ?? new InMemorySessionStore());

    private static DashboardSnapshot MakeSnapshot(DashboardPeriod period, decimal revenue,
        DashboardSourceKind sourceKind = DashboardSourceKind.Preview)
    {
        var date = period switch
        {
            DashboardPeriod.Today => new DateOnly(2026, 10, 14),
            DashboardPeriod.Last7Days => new DateOnly(2026, 10, 8),
            _ => new DateOnly(2026, 10, 1)
        };
        var end = new DateOnly(2026, 10, 14);
        return new DashboardSnapshot(period, date, end,
            new DateTimeOffset(2026, 10, 14, 17, 30, 0, TimeSpan.FromHours(7)), sourceKind,
            new DashboardSummary(revenue, 1, 1, 0, null, null),
            [new DailyRevenuePoint(end, revenue)], [], []);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout) throw new TimeoutException("Dashboard state did not reach the expected value.");
            await Task.Delay(10);
        }
    }

    private sealed class AnalyticsRepositoryFake(IAnalyticsRepository inner) : IAnalyticsRepository
    {
        public List<DashboardQuery> Queries { get; } = [];
        public async Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return await inner.GetDashboardAsync(query, cancellationToken);
        }
    }

    private sealed class SequencedAnalyticsRepository(
        TaskCompletionSource<DashboardReadResult> first,
        TaskCompletionSource<DashboardReadResult> second) : IAnalyticsRepository
    {
        private int _calls;
        public List<DashboardQuery> Queries { get; } = [];
        public CancellationToken LastToken { get; private set; }
        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            LastToken = cancellationToken;
            return Interlocked.Increment(ref _calls) == 1 ? first.Task : second.Task;
        }
    }

    private sealed class FailOnceAnalyticsRepository : IAnalyticsRepository
    {
        private int _calls;
        public DashboardQuery? LastQuery { get; private set; }
        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            var call = Interlocked.Increment(ref _calls);
            return call == 2
                ? Task.FromException<DashboardReadResult>(new InvalidOperationException("temporary test failure"))
                : Task.FromResult(DashboardReadResult.Available(MakeSnapshot(query.Period, call == 1 ? 9_876m : 1_234m)));
        }
    }

    private sealed class RecordingAnalyticsRepository : IAnalyticsRepository
    {
        public List<DashboardQuery> Queries { get; } = [];
        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            var snapshot = MakeSnapshot(query.Period, 99m, DashboardSourceKind.Live);
            if (query.Range is { } range)
                snapshot = snapshot with { StartDate = range.From, EndDate = range.To };
            return Task.FromResult(DashboardReadResult.Available(snapshot));
        }
    }

    private sealed class ThrowingAnalyticsRepository(ApiException exception) : IAnalyticsRepository
    {
        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default) =>
            Task.FromException<DashboardReadResult>(exception);
    }

    private sealed class UncooperativeAnalyticsRepository(
        TaskCompletionSource started,
        TaskCompletionSource<DashboardReadResult> completion) : IAnalyticsRepository
    {
        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            return completion.Task;
        }
    }
}
