using System.Collections;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.Repositories.Analytics;

public sealed class FixtureAnalyticsRepositoryTests
{
    private readonly FixtureAnalyticsRepository _repository = new();

    [Theory]
    [InlineData(DashboardPeriod.Today, "2026-10-14", "2026-10-14")]
    [InlineData(DashboardPeriod.Last7Days, "2026-10-08", "2026-10-14")]
    [InlineData(DashboardPeriod.ThisMonth, "2026-10-01", "2026-10-14")]
    public async Task GetDashboardAsync_ReturnsStablePeriodAndRevenueTotal(
        DashboardPeriod period,
        string expectedStart,
        string expectedEnd)
    {
        var result = await _repository.GetDashboardAsync(new DashboardQuery(period));

        Assert.True(result.IsAvailable);
        var snapshot = Assert.IsType<DashboardSnapshot>(result.Snapshot);
        Assert.Equal(DateOnly.Parse(expectedStart), snapshot.StartDate);
        Assert.Equal(DateOnly.Parse(expectedEnd), snapshot.EndDate);
        Assert.Equal(snapshot.StartDate, snapshot.DailyRevenue[0].Date);
        Assert.Equal(snapshot.EndDate, snapshot.DailyRevenue[^1].Date);
        Assert.Equal(snapshot.Summary.Revenue, snapshot.DailyRevenue.Sum(point => point.Revenue));
        Assert.All(snapshot.DailyRevenue, point => Assert.True(point.Revenue >= 0m));
        Assert.Equal("dashboard-preview-v1", FixtureAnalyticsRepository.FixtureVersion);
        Assert.Equal(TimeSpan.FromHours(7), snapshot.AsOf.Offset);
    }

    [Fact]
    public async Task GetDashboardAsync_DoesNotDeriveTotalsFromPreviewRows_AndReturnsReadOnlyCollections()
    {
        var result = await _repository.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Last7Days));
        var snapshot = Assert.IsType<DashboardSnapshot>(result.Snapshot);

        Assert.True(snapshot.Summary.OrderCount > snapshot.RecentOrders.Count);
        Assert.True(snapshot.Summary.LowStockCount > snapshot.LowStockItems.Count);
        Assert.Equal(5, snapshot.RecentOrders.Count);
        Assert.Equal(5, snapshot.LowStockItems.Count);
        Assert.True(Assert.IsAssignableFrom<IList>(snapshot.RecentOrders).IsReadOnly);
        Assert.Null((await _repository.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today))).Snapshot!.Summary.RevenueChangePercent);
        Assert.Contains(snapshot.RecentOrders, order => order.Status == OrderStatus.Processing);
        Assert.Contains(snapshot.RecentOrders, order => order.Status == OrderStatus.Pending);
        Assert.Contains(snapshot.RecentOrders, order => order.SerialLabel is null);

        var today = (await _repository.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today))).Snapshot!;
        Assert.Equal(2, today.RecentOrders.Count);
        Assert.All(today.RecentOrders, order => Assert.Equal(today.EndDate, DateOnly.FromDateTime(order.CreatedAt.Date)));
    }

    [Fact]
    public async Task GetDashboardAsync_ThrowsForCancelledRequestAndUnknownPeriod()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _repository.GetDashboardAsync(new DashboardQuery(DashboardPeriod.Today), source.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.GetDashboardAsync(new DashboardQuery((DashboardPeriod)999)));
    }

    [Fact]
    public void UnavailableResult_IsDistinctFromAnAvailableZeroSnapshot()
    {
        Assert.False(DashboardReadResult.Unavailable.IsAvailable);
        Assert.Null(DashboardReadResult.Unavailable.Snapshot);

        var zeroSnapshot = new DashboardSnapshot(
            DashboardPeriod.Today,
            new DateOnly(2026, 10, 14),
            new DateOnly(2026, 10, 14),
            new DateTimeOffset(2026, 10, 14, 17, 30, 0, TimeSpan.FromHours(7)),
            DashboardSourceKind.Demo,
            new DashboardSummary(0m, 0, 0, 0, null, null),
            Array.Empty<DailyRevenuePoint>(),
            Array.Empty<LowStockItem>(),
            Array.Empty<RecentOrder>());

        var available = DashboardReadResult.Available(zeroSnapshot);
        Assert.True(available.IsAvailable);
        Assert.Equal(0m, available.Snapshot!.Summary.Revenue);
    }
}
