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

        var response = await service.GetDashboardAsync(new(period));
        var result = Assert.IsType<AdminDashboardDto>(response.Snapshot);

        Assert.Equal(DateOnly.Parse(expectedStart), result.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 18), result.EndDate);
        Assert.Equal("UTC+07:00", result.TimeZone);
        Assert.Equal(DateTimeKind.Utc, result.AsOf.Kind);
        Assert.Equal(result.AsOf, repo.Criteria!.ToExclusiveUtc);
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

        var result = Assert.IsType<AdminDashboardDto>((await service.GetDashboardAsync(new("today"))).Snapshot);

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

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardAsync(new("today")));

        Assert.Same(expected, actual);
        Assert.Equal(1, uow.RollbackCount);
        Assert.Equal(0, uow.CommitCount);
    }

    [Theory]
    [InlineData("2026-10-12", "2026-10-12", 12, 1)]
    [InlineData("2026-10-12", "2026-10-14", 14, 3)]
    [InlineData("2026-10-16", "2026-10-18", 17, 3)]
    public async Task CustomDashboard_SeparatesSnapshotTimeFromReportBoundaries(string from, string to,
        int endUtcDay, int days)
    {
        var asOf = new DateTime(2026, 10, 17, 17, 30, 0, DateTimeKind.Utc);
        var start = DateOnly.Parse(from);
        var end = DateOnly.Parse(to);
        var repo = new FakeAnalyticsRepository(asOf)
        {
            ReadModel = new(890m, 1, 5, 0, [new(end, 890m)], [], [])
        };
        var uow = new FakeUnitOfWork();
        var service = new AnalyticsService(repo, uow);

        var result = Assert.IsType<AdminDashboardDto>((await service.GetDashboardAsync(new("custom", (start, end)))).Snapshot);

        Assert.Equal(start, result.StartDate);
        Assert.Equal(end, result.EndDate);
        Assert.Equal(asOf, result.AsOf);
        Assert.Equal(start.AddDays(-1).ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc), repo.Criteria!.FromUtc);
        var expectedEndUtc = new DateTime(2026, 10, endUtcDay, 17, end.Day == 18 ? 30 : 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedEndUtc, repo.Criteria.ToExclusiveUtc);
        Assert.Equal(days, result.DailyRevenue.Count);
        Assert.Equal(end, result.DailyRevenue[^1].Date);
        Assert.Equal(result.Summary.Revenue, result.DailyRevenue.Sum(point => point.Revenue));
        Assert.All(result.DailyRevenue.SkipLast(1), point => Assert.Equal(0m, point.Revenue));
        Assert.Equal(1, uow.CommitCount);
    }

    [Theory]
    [InlineData("0001-01-01", "0001-01-02", "from")]
    [InlineData("9999-12-31", "9999-12-31", "to")]
    [InlineData("2026-10-18", "2026-10-19", "to")]
    public async Task CustomDashboard_WhenBoundariesInvalid_DoesNotAggregate(string from, string to, string field)
    {
        var repo = new FakeAnalyticsRepository(new DateTime(2026, 10, 17, 17, 30, 0, DateTimeKind.Utc));
        var uow = new FakeUnitOfWork();

        var result = await new AnalyticsService(repo, uow)
            .GetDashboardAsync(new("custom", (DateOnly.Parse(from), DateOnly.Parse(to))));

        Assert.Equal(field, result.Error?.Field);
        Assert.Null(result.Snapshot);
        Assert.Null(repo.Criteria);
        Assert.Equal(1, uow.CommitCount);
    }

    private sealed class FakeAnalyticsRepository(DateTime asOfUtc) : IAnalyticsRepository
    {
        public DashboardAnalyticsReadModel ReadModel { get; init; } = new(0m, 0, 0, 0, [], [], []);
        public Exception? Failure { get; init; }
        public DashboardAnalyticsCriteria? Criteria { get; private set; }

        public Task<DateTime> GetSnapshotTimeUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(asOfUtc);

        public Task<DashboardAnalyticsReadModel> GetDashboardAsync(DashboardAnalyticsCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            Criteria = criteria;
            Assert.Equal(DateTimeKind.Utc, criteria.FromUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, criteria.ToExclusiveUtc.Kind);
            return Task.FromResult(ReadModel);
        }
    }
}
