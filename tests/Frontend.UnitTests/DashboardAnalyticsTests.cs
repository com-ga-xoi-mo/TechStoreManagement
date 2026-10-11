using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.ViewModels.Dashboard;
using Xunit;

namespace TechStore.UnitTests.Client.Analytics;

public class DashboardAnalyticsTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 10, 15, 30, 0, TimeSpan.FromHours(7));

    [Fact]
    public void PeriodRanges_AreLocalCalendarIntervalsWithExclusiveEnd()
    {
        DashboardPeriodHelper.ToHoChiMinhTime(new DateTimeOffset(2026, 10, 9, 17, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.FromHours(7)));
        var today = DashboardPeriodHelper.GetPeriodRange(DashboardPeriod.Today, FixedNow);
        today.Start.Should().Be(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.FromHours(7)));
        today.EndExclusive.Should().Be(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.FromHours(7)));

        var sevenDays = DashboardPeriodHelper.GetPeriodRange(DashboardPeriod.Last7Days, FixedNow);
        sevenDays.Start.Should().Be(new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.FromHours(7)));
        (sevenDays.EndExclusive - sevenDays.Start).Should().Be(TimeSpan.FromDays(7));

        var month = DashboardPeriodHelper.GetPeriodRange(DashboardPeriod.ThisMonth, FixedNow);
        month.Start.Should().Be(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(7)));
        month.EndExclusive.Should().Be(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7)));
        DashboardPeriodHelper.GetComparisonRange(month.Start, month.EndExclusive)
            .Should().Be((new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)), month.Start));
    }

    [Fact]
    public async Task PopulatedSnapshot_ReconcilesRevenueChartAndRecentOrders()
    {
        var repository = new FakeAnalyticsRepository(clock: () => FixedNow);

        var snapshot = await repository.GetDashboardSnapshotAsync(DashboardPeriod.Last7Days);

        snapshot.Revenue.Should().Be(snapshot.RevenuePoints.Sum(point => point.Revenue));
        snapshot.CompletedOrderCount.Should().BeGreaterThan(snapshot.RecentOrders.Count);
        snapshot.RecentOrders.Should().HaveCount(5).And.BeInDescendingOrder(order => order.CreatedAt);
        (snapshot.RangeEndExclusive > snapshot.RangeStart).Should().BeTrue();
        snapshot.TotalActiveProducts.Should().Be(8);
        snapshot.Provenance.Should().Be(DashboardDataProvenance.SyntheticPreview);
        snapshot.LowStockCount.Should().Be(snapshot.LowStockItems.Count);
        snapshot.LowStockItems.Should().OnlyContain(item => item.AvailableQuantity <= item.MinimumStockAlert);
        snapshot.RecentOrders.Should().OnlyContain(order =>
            order.CreatedAt >= snapshot.RangeStart && order.CreatedAt < snapshot.RangeEndExclusive);
    }

    [Fact]
    public async Task EmptyFixture_KeepsCurrentInventorySnapshot()
    {
        var repository = new FakeAnalyticsRepository(DashboardFixtureScenario.Empty, () => FixedNow);

        var snapshot = await repository.GetDashboardSnapshotAsync(DashboardPeriod.Today);

        snapshot.HasSales.Should().BeFalse();
        snapshot.Revenue.Should().Be(0m);
        snapshot.CompletedOrderCount.Should().Be(0);
        snapshot.TotalActiveProducts.Should().Be(8);
        snapshot.LowStockItems.Should().NotBeEmpty();
        snapshot.RevenuePoints.Should().OnlyContain(point => point.Revenue == 0m);
    }

    [Fact]
    public async Task OnePointFixture_ProvidesOneDatedRevenueBucketAndMatchingOrder()
    {
        var repository = new FakeAnalyticsRepository(DashboardFixtureScenario.OnePoint, () => FixedNow);

        var snapshot = await repository.GetDashboardSnapshotAsync(DashboardPeriod.Today);

        snapshot.RevenuePoints.Should().ContainSingle();
        snapshot.RevenuePoints[0].Revenue.Should().Be(snapshot.Revenue);
        snapshot.CompletedOrderCount.Should().Be(1);
        snapshot.RecentOrders.Should().ContainSingle();
        snapshot.RecentOrders[0].CreatedAt.Should().Be(snapshot.RevenuePoints[0].BucketStart);
    }

    [Fact]
    public async Task PriorPeriodZero_HasNoPercentageAndErrorFixtureFails()
    {
        var emptyRepository = new FakeAnalyticsRepository(DashboardFixtureScenario.Empty, () => FixedNow);
        var empty = await emptyRepository.GetDashboardSnapshotAsync(DashboardPeriod.Last7Days);
        empty.RevenueChangePercent.Should().BeNull();
        empty.OrderCountChangePercent.Should().BeNull();

        var errorRepository = new FakeAnalyticsRepository(DashboardFixtureScenario.Error, () => FixedNow);
        var act = () => errorRepository.GetDashboardSnapshotAsync(DashboardPeriod.Today);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DelayedFixture_HonorsCancellation()
    {
        var repository = new FakeAnalyticsRepository(DashboardFixtureScenario.Delayed, () => FixedNow);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var act = () => repository.GetDashboardSnapshotAsync(DashboardPeriod.Today, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void DebugFixtureSetting_OnlyAppliesInExplicitPreview()
    {
        DashboardPreviewSettings.Resolve(true, true, "empty").Should().Be(DashboardFixtureScenario.Empty);
        DashboardPreviewSettings.Resolve(true, true, "delayed").Should().Be(DashboardFixtureScenario.Delayed);
        DashboardPreviewSettings.Resolve(true, true, "error").Should().Be(DashboardFixtureScenario.Error);
        DashboardPreviewSettings.Resolve(true, true, "onepoint").Should().Be(DashboardFixtureScenario.OnePoint);
        DashboardPreviewSettings.Resolve(true, false, "error").Should().Be(DashboardFixtureScenario.Populated);
        DashboardPreviewSettings.Resolve(false, true, "error").Should().Be(DashboardFixtureScenario.Populated);
    }

    [Fact]
    public async Task ViewModel_LoadsDefaultRangeAndFormatsCurrency()
    {
        var repository = new FakeAnalyticsRepository(clock: () => FixedNow);
        var viewModel = new DashboardViewModel(repository);

        await viewModel.InitializeAsync();

        viewModel.SelectedPeriod.Should().Be(DashboardPeriod.Last7Days);
        viewModel.Snapshot.Should().NotBeNull();
        viewModel.Snapshot!.Period.Should().Be(DashboardPeriod.Last7Days);
        viewModel.RevenueText.Should().EndWith(" ₫");
        viewModel.OrderCountText.Should().NotBeEmpty();
        viewModel.ProductCountText.Should().Be("8");
        viewModel.LowStockCountText.Should().NotBeEmpty();
        viewModel.HasData.Should().BeTrue();
        viewModel.KpiCards.Should().HaveCount(4);
        viewModel.RevenueCard.Value.Should().Be(viewModel.RevenueText);
        viewModel.ChartPointLabels.Should().NotBeEmpty();
        viewModel.ChartPointLabels[0].Text.Should().StartWith("04/10:");
        viewModel.LowStockItems.Should().NotBeEmpty();
        viewModel.RecentOrders.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ViewModel_IgnoresAnOlderRequestThatCompletesLast()
    {
        var repository = new OutOfOrderRepository();
        var viewModel = new DashboardViewModel(repository);
        var initialize = viewModel.InitializeAsync();
        await repository.WaitForRequestAsync(DashboardPeriod.Last7Days);
        repository.Complete(DashboardPeriod.Last7Days);
        await initialize;

        var oldRequest = viewModel.ChangePeriodCommand.ExecuteAsync(DashboardPeriod.Today);
        await repository.WaitForRequestAsync(DashboardPeriod.Today);
        var newRequest = viewModel.ChangePeriodCommand.ExecuteAsync(DashboardPeriod.ThisMonth);
        await repository.WaitForRequestAsync(DashboardPeriod.ThisMonth);
        repository.Complete(DashboardPeriod.ThisMonth);
        await newRequest;
        repository.Complete(DashboardPeriod.Today);
        await oldRequest;

        viewModel.SelectedPeriod.Should().Be(DashboardPeriod.ThisMonth);
        viewModel.Snapshot!.Period.Should().Be(DashboardPeriod.ThisMonth);
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ViewModel_DoesNotPublishRequestFromPreviousPageActivation()
    {
        var repository = new QueuedRepository();
        var viewModel = new DashboardViewModel(repository);
        var firstActivation = viewModel.InitializeAsync();
        await repository.WaitForCallsAsync(1);
        repository.Complete(0, DashboardPeriod.Last7Days, 10m);
        await firstActivation;

        var staleRequest = viewModel.ChangePeriodCommand.ExecuteAsync(DashboardPeriod.Today);
        await repository.WaitForCallsAsync(2);
        viewModel.Deactivate();
        var currentActivation = viewModel.ActivateAsync();
        await repository.WaitForCallsAsync(3);
        repository.Complete(2, DashboardPeriod.Today, 30m);
        await currentActivation;
        repository.Complete(1, DashboardPeriod.Today, 20m);
        await staleRequest;

        viewModel.Snapshot!.Revenue.Should().Be(30m);
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ViewModel_ReportsFailureAndRetriesSelectedRange()
    {
        var repository = new FailOnceRepository(new FakeAnalyticsRepository(clock: () => FixedNow));
        var viewModel = new DashboardViewModel(repository);
        await viewModel.InitializeAsync();
        viewModel.IsError.Should().BeTrue();
        viewModel.ErrorMessage.Should().NotBeEmpty();

        await viewModel.RetryCommand.ExecuteAsync(null);

        viewModel.IsError.Should().BeFalse();
        viewModel.Snapshot!.Period.Should().Be(DashboardPeriod.Last7Days);
        repository.RequestedPeriods.Should().Equal(DashboardPeriod.Last7Days, DashboardPeriod.Last7Days);
    }

    [Fact]
    public async Task EmptySnapshot_UsesZeroAndUnavailableComparisonText()
    {
        var repository = new FakeAnalyticsRepository(DashboardFixtureScenario.Empty, () => FixedNow);
        var viewModel = new DashboardViewModel(repository);

        await viewModel.InitializeAsync();

        viewModel.RevenueText.Should().Be("0 ₫");
        viewModel.RevenueComparisonText.Should().Be("Chưa có kỳ so sánh");
        viewModel.IsEmpty.Should().BeTrue();
        viewModel.ProductCountCard.Detail.Should().Contain("hiện tại");
    }

    [Fact]
    public async Task ViewModel_UsesVietnameseLocalTimeLabelsForToday()
    {
        var viewModel = new DashboardViewModel(new FakeAnalyticsRepository(clock: () => FixedNow));

        await viewModel.InitializeAsync();
        await viewModel.ChangePeriodCommand.ExecuteAsync(DashboardPeriod.Today);

        viewModel.ChartPointLabels[0].Text.Should().StartWith("00:00:");
        viewModel.RangeText.Should().Be("10/10 – 10/10");
    }

    private sealed class FailOnceRepository(IAnalyticsRepository inner) : IAnalyticsRepository
    {
        private int _requests;
        public System.Collections.Generic.List<DashboardPeriod> RequestedPeriods { get; } = [];

        public Task<DashboardSnapshot> GetDashboardSnapshotAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
        {
            RequestedPeriods.Add(period);
            if (Interlocked.Increment(ref _requests) == 1)
            {
                return Task.FromException<DashboardSnapshot>(new InvalidOperationException("Temporary fixture failure."));
            }

            return inner.GetDashboardSnapshotAsync(period, cancellationToken);
        }
    }

    private sealed class OutOfOrderRepository : IAnalyticsRepository
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<DashboardPeriod, TaskCompletionSource<DashboardSnapshot>> _requests = new();

        public Task<DashboardSnapshot> GetDashboardSnapshotAsync(DashboardPeriod period, CancellationToken cancellationToken = default) =>
            _requests.GetOrAdd(period, _ => new TaskCompletionSource<DashboardSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

        public async Task WaitForRequestAsync(DashboardPeriod period)
        {
            for (var attempt = 0; attempt < 100 && !_requests.ContainsKey(period); attempt++)
            {
                await Task.Delay(5);
            }

            _requests.ContainsKey(period).Should().BeTrue();
        }

        public void Complete(DashboardPeriod period)
        {
            var (start, end) = DashboardPeriodHelper.GetPeriodRange(period, FixedNow);
            var snapshot = new DashboardSnapshot(FixedNow, period, start, end, 10m, 0m, null, 1, 0, null,
                8, 1, [new RevenuePoint(start, 10m)], [], []);
            _requests[period].TrySetResult(snapshot).Should().BeTrue();
        }
    }

    private sealed class QueuedRepository : IAnalyticsRepository
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<TaskCompletionSource<DashboardSnapshot>> _requests = new();
        public int Count => _requests.Count;

        public Task<DashboardSnapshot> GetDashboardSnapshotAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
        {
            var request = new TaskCompletionSource<DashboardSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Enqueue(request);
            return request.Task;
        }

        public async Task WaitForCallsAsync(int count)
        {
            for (var attempt = 0; attempt < 100 && Count < count; attempt++)
            {
                await Task.Delay(5);
            }

            Count.Should().BeGreaterThanOrEqualTo(count);
        }

        public void Complete(int index, DashboardPeriod period, decimal revenue)
        {
            var all = _requests.ToArray();
            var (start, end) = DashboardPeriodHelper.GetPeriodRange(period, FixedNow);
            var snapshot = new DashboardSnapshot(FixedNow, period, start, end, revenue, 0m, null, 1, 0, null,
                8, 1, [new RevenuePoint(start, revenue)], [], []);
            all[index].TrySetResult(snapshot).Should().BeTrue();
        }
    }
}
