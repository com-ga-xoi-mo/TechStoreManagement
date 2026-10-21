using System.Data;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Analytics.Services;
using TechStore.Shared.DTOs;

namespace TechStore.UnitTests;

public sealed class AnalyticsServiceTests
{
    [Theory]
    [InlineData("today", "2026-10-18")]
    [InlineData("last7Days", "2026-10-12")]
    [InlineData("thisMonth", "2026-10-01")]
    public async Task GetDashboardAsync_UsesVietnamCalendarAndFillsZeroDays(string period, string expectedStart)
    {
        var repo = new FakeAnalyticsRepository(new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc));
        var uow = new FakeUnitOfWork();
        var service = new AnalyticsService(repo, uow);

        var result = await service.GetDashboardAsync(period);

        Assert.Equal(DateOnly.Parse(expectedStart), result.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 18), result.EndDate);
        Assert.Equal("UTC+07:00", result.TimeZone);
        Assert.Equal(DateTimeKind.Utc, result.AsOf.Kind);
        Assert.Equal((result.EndDate.DayNumber - result.StartDate.DayNumber) + 1, result.DailyRevenue.Count);
        Assert.Equal(0m, result.DailyRevenue[^1].Revenue);
        Assert.Null(result.Summary.RevenueChangePercent);
        Assert.Null(result.Summary.OrderCountChangePercent);
        Assert.Equal(IsolationLevel.RepeatableRead, uow.LastIsolationLevel);
        Assert.Equal(1, uow.CommitCount);
        Assert.Equal(0, uow.RollbackCount);
    }

    [Fact]
    public async Task GetDashboardAsync_MapsBoundedRowsAndAvailableQuantity()
    {
        var repo = new FakeAnalyticsRepository(new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc))
        {
            ReadModel = new DashboardAnalyticsReadModel(125m, 3, 8, 1,
                [new DashboardDailyRevenueRow(new DateOnly(2026, 10, 18), 125m)],
                [new DashboardLowStockRow(Guid.NewGuid(), Guid.NewGuid(), "Phone", null, null, 5, 2, 5)],
                [new DashboardRecentOrderRow(Guid.NewGuid(), "ORD-1", null, "Phone", "Blue", 2, 125m, "Completed", new DateTime(2026, 10, 18, 2, 0, 0, DateTimeKind.Utc))])
        };
        var service = new AnalyticsService(repo, new FakeUnitOfWork());

        var result = await service.GetDashboardAsync("today");

        Assert.Equal(3, result.LowStockItems[0].AvailableQuantity);
        Assert.Null(result.RecentOrders[0].CustomerName);
        Assert.Equal("Phone - Blue + 1 mặt hàng", result.RecentOrders[0].ProductSummary);
        Assert.Equal(125m, result.Summary.Revenue);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenRepositoryFails_RollsBackAndRethrows()
    {
        var expected = new InvalidOperationException("database failure");
        var repo = new FakeAnalyticsRepository(DateTime.UtcNow) { Failure = expected };
        var uow = new FakeUnitOfWork();
        var service = new AnalyticsService(repo, uow);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardAsync("today"));

        Assert.Same(expected, actual);
        Assert.Equal(1, uow.RollbackCount);
        Assert.Equal(0, uow.CommitCount);
    }

    private sealed class FakeAnalyticsRepository(DateTime asOfUtc) : IAnalyticsRepository
    {
        public DashboardAnalyticsReadModel ReadModel { get; init; } = new(0m, 0, 0, 0, [], [], []);
        public Exception? Failure { get; init; }

        public Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(asOfUtc);

        public Task<DashboardAnalyticsReadModel> GetDashboardAsync(DashboardAnalyticsCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            Assert.Equal(DateTimeKind.Utc, criteria.FromUtc.Kind);
            Assert.Equal(asOfUtc, criteria.AsOfUtc);
            return Task.FromResult(ReadModel);
        }
    }
}
