using TechStore.Client.Models.Analytics;
using TechStore.Shared.Enums;

namespace TechStore.Client.Repositories.Analytics;

/// <summary>Deterministic display data for the explicitly enabled Dashboard preview.</summary>
public sealed class FixtureAnalyticsRepository : IAnalyticsRepository
{
    public const string FixtureVersion = "dashboard-preview-v1";
    public static readonly DateOnly ReferenceDate = new(2026, 10, 14);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static readonly LowStockItem[] LowStock =
    [
        new("iPhone 15 Pro Max", "256GB Titan", 2, 0, 2, 5),
        new("MacBook Air M3", "13 inch · 16GB", 1, 0, 1, 4),
        new("Galaxy S24 Ultra", "512GB Gray", 3, 0, 3, 6),
        new("iPad Air M2", "11 inch Wi-Fi", 2, 0, 2, 5),
        new("Asus Zenbook 14 OLED UX3405", "16GB RAM", 1, 0, 1, 3)
    ];

    private static readonly RecentOrder[] Recent =
    [
        new("DEMO-8921", "Nguyễn Văn An", "iPhone 15 Pro Max 256GB Titan Tự Nhiên", 34_990_000m, OrderStatus.Completed, At(14, 10, 42)),
        new("DEMO-8920", "Trần Thị Mai", "MacBook Air M3 13 inch 16GB / 512GB", 32_490_000m, OrderStatus.Processing, At(14, 9, 18)),
        new("DEMO-8919", "Lê Hoàng Long", "Samsung Galaxy S24 Ultra 512GB", 29_990_000m, OrderStatus.Pending, At(13, 17, 5)),
        new("DEMO-8918", "Phạm Minh Đức", "Asus Zenbook 14 OLED UX3405", 26_890_000m, OrderStatus.Completed, At(12, 15, 31)),
        new("DEMO-8917", "Vũ Quỳnh Trang", "Tai nghe AirPods Pro 2 USB-C", 5_890_000m, OrderStatus.Completed, At(11, 11, 12))
    ];

    public Task<DashboardReadResult> GetDashboardAsync(
        DashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var (start, end) = query.Period switch
        {
            DashboardPeriod.Today => (ReferenceDate, ReferenceDate),
            DashboardPeriod.Last7Days => (ReferenceDate.AddDays(-6), ReferenceDate),
            DashboardPeriod.ThisMonth => (new DateOnly(ReferenceDate.Year, ReferenceDate.Month, 1), ReferenceDate),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Period, "Unknown Dashboard period.")
        };

        var dailyRevenue = BuildRevenue(query.Period, start, end);
        var revenue = dailyRevenue.Sum(point => point.Revenue);
        var orderCount = query.Period switch
        {
            DashboardPeriod.Today => 12,
            DashboardPeriod.Last7Days => 128,
            DashboardPeriod.ThisMonth => 412,
            _ => 0
        };
        var productCount = 1_420;
        var lowStockCount = 7;
        var asOf = new DateTimeOffset(ReferenceDate, new TimeOnly(17, 30), VietnamOffset);
        var summary = new DashboardSummary(
            revenue,
            orderCount,
            productCount,
            lowStockCount,
            RevenueChangePercent: query.Period == DashboardPeriod.Today ? null : 12.5m,
            OrderCountChangePercent: query.Period == DashboardPeriod.Today ? null : 8.2m);
        var snapshot = new DashboardSnapshot(
            query.Period,
            start,
            end,
            asOf,
            DashboardSourceKind.Preview,
            summary,
            Array.AsReadOnly(dailyRevenue),
            Array.AsReadOnly(LowStock.ToArray()),
            Array.AsReadOnly(Recent
                .Where(order => order.CreatedAt.Date >= StartOfDay(start) && order.CreatedAt.Date < StartOfDay(end.AddDays(1)))
                .ToArray()));

        return Task.FromResult(DashboardReadResult.Available(snapshot));
    }

    private static DailyRevenuePoint[] BuildRevenue(DashboardPeriod period, DateOnly start, DateOnly end)
    {
        var dailyValues = period switch
        {
            DashboardPeriod.Today => new decimal[] { 92_650_000m },
            DashboardPeriod.Last7Days => new[] { 35_700_000m, 51_200_000m, 40_100_000m, 45_600_000m, 57_800_000m, 64_300_000m, 92_650_000m },
            DashboardPeriod.ThisMonth => BuildMonthValues(),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };

        var points = new DailyRevenuePoint[dailyValues.Length];
        for (var index = 0; index < dailyValues.Length; index++)
            points[index] = new DailyRevenuePoint(start.AddDays(index), dailyValues[index]);

        if (points.Length == 0 || points[^1].Date != end)
            throw new InvalidOperationException("Fixture revenue dates must cover the requested period.");
        return points;
    }

    private static decimal[] BuildMonthValues()
    {
        var values = new decimal[14];
        values[0] = 51_200_000m;
        values[1] = 40_100_000m;
        values[2] = 0m;
        values[3] = 57_800_000m;
        values[4] = 64_300_000m;
        values[5] = 92_650_000m;
        values[6] = 35_700_000m;
        values[7] = 35_700_000m;
        values[8] = 51_200_000m;
        values[9] = 40_100_000m;
        values[10] = 45_600_000m;
        values[11] = 57_800_000m;
        values[12] = 64_300_000m;
        values[13] = 92_650_000m;
        return values;
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(ReferenceDate.Year, ReferenceDate.Month, day, hour, minute, 0, VietnamOffset);

    private static DateTimeOffset StartOfDay(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 0, 0, 0, VietnamOffset);
}
