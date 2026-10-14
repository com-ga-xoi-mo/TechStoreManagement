using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Development;
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
    public async Task SelectPeriodAsync_UsesRepositoryAndUnavailableDoesNotBecomeZeroContent()
    {
        var repository = new AnalyticsRepositoryFake(new UnavailableAnalyticsRepository());
        var viewModel = Create(repository, demoEnabled: false);
        await viewModel.ActivateAsync();

        await viewModel.SelectPeriodCommand.ExecuteAsync(0);

        Assert.Equal(DashboardPeriod.Today, viewModel.SelectedPeriod);
        Assert.Equal(PageContentState.Empty, viewModel.ContentState);
        Assert.True(viewModel.IsUnavailable);
        Assert.False(viewModel.CanChangePeriod);
        Assert.Null(viewModel.Snapshot);
        Assert.Equal("—", viewModel.LowStockText);
        Assert.Empty(viewModel.LowStockTrendText);
        Assert.Equal("Chưa có dữ liệu tồn kho", viewModel.LowStockDetailText);
        Assert.Equal(2, repository.Queries.Count);
    }

    [Fact]
    public async Task SelectPeriodAsync_LatestRequestOwnsSnapshotAndState()
    {
        var firstCompletion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<DashboardReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new SequencedAnalyticsRepository(firstCompletion, secondCompletion);
        var viewModel = Create(repository);
        var initial = viewModel.ActivateAsync();
        Assert.Equal(DashboardPeriod.Last7Days, repository.Queries[0].Period);

        var first = viewModel.SelectPeriodCommand.ExecuteAsync(0);
        var second = viewModel.SelectPeriodCommand.ExecuteAsync(2);
        secondCompletion.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.ThisMonth, 222m)));
        await second;

        firstCompletion.SetResult(DashboardReadResult.Available(MakeSnapshot(DashboardPeriod.Today, 111m)));
        await Task.WhenAll(first, initial);

        Assert.Equal(DashboardPeriod.ThisMonth, viewModel.SelectedPeriod);
        Assert.Equal(222m, viewModel.Snapshot!.Summary.Revenue);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
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

    private static DashboardViewModel Create(IAnalyticsRepository repository, bool demoEnabled = true) =>
        new(repository, new DashboardPreviewOptions(demoEnabled));

    private static DashboardSnapshot MakeSnapshot(DashboardPeriod period, decimal revenue)
    {
        var date = period switch
        {
            DashboardPeriod.Today => new DateOnly(2026, 10, 14),
            DashboardPeriod.Last7Days => new DateOnly(2026, 10, 8),
            _ => new DateOnly(2026, 10, 1)
        };
        var end = new DateOnly(2026, 10, 14);
        return new DashboardSnapshot(period, date, end,
            new DateTimeOffset(2026, 10, 14, 17, 30, 0, TimeSpan.FromHours(7)), DashboardSourceKind.Demo,
            new DashboardSummary(revenue, 1, 1, 0, null, null),
            [new DailyRevenuePoint(end, revenue)], [], []);
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
        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Interlocked.Increment(ref _calls) switch
            {
                1 => first.Task,
                2 => first.Task,
                _ => second.Task
            };
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
