using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Repositories.Analytics;

public enum DashboardFixtureScenario
{
    Populated,
    Empty,
    Delayed,
    Error,
    OnePoint
}

/// <summary>Deterministic synthetic analytics for the explicit UI preview.</summary>
public sealed class FakeAnalyticsRepository : IAnalyticsRepository
{
    private readonly DashboardFixtureScenario _scenario;
    private readonly Func<DateTimeOffset> _clock;

    private static readonly StockRecord[] Stock =
    [
        new("P-001", "iPhone 15 Pro Max", "256GB Titan", 2, 0, 5, "máy"),
        new("P-002", "MacBook Air M3", "13 inch 16GB", 1, 0, 3, "máy"),
        new("P-003", "Galaxy S24 Ultra", "512GB Gray", 8, 5, 3, "máy"),
        new("P-004", "iPad Air M2", "11 inch Wi-Fi", 2, 0, 4, "máy"),
        new("P-005", "Asus Zenbook 14", "OLED UX3405", 1, 0, 2, "máy"),
        new("P-006", "AirPods Pro 2", "USB-C", 12, 1, 4, "cái"),
        new("P-007", "Keychron K8", "Brown Switch", 3, 0, 3, "cái"),
        new("P-008", "Logitech MX Master 3S", "Graphite", 9, 0, 3, "cái")
    ];

    private static readonly string[] Customers = ["Nguyễn Minh An", "Trần Thu Hà", "Lê Hoàng Long", "Phạm Gia Bảo", "Vũ Quỳnh Trang"];

    public FakeAnalyticsRepository(
        DashboardFixtureScenario scenario = DashboardFixtureScenario.Populated,
        Func<DateTimeOffset>? clock = null)
    {
        _scenario = scenario;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<DashboardSnapshot> GetDashboardSnapshotAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken = default)
    {
        var delay = _scenario == DashboardFixtureScenario.Delayed ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(120);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        if (_scenario == DashboardFixtureScenario.Error)
        {
            throw new InvalidOperationException("Mock dashboard failure.");
        }

        var now = _clock();
        var (rangeStart, rangeEndExclusive) = DashboardPeriodHelper.GetPeriodRange(period, now);
        var (previousStart, previousEndExclusive) = DashboardPeriodHelper.GetComparisonRange(rangeStart, rangeEndExclusive);
        var lowStockItems = Stock
            .Where(item => item.Quantity - item.ReservedQuantity <= item.MinimumStockAlert)
            .Select(item => new LowStockItem(item.ProductId, item.ProductName, item.VariantName,
                item.Quantity - item.ReservedQuantity, item.MinimumStockAlert, item.Unit))
            .ToArray();

        if (_scenario == DashboardFixtureScenario.Empty)
        {
            return new DashboardSnapshot(
                GeneratedAt: now,
                Period: period,
                RangeStart: rangeStart,
                RangeEndExclusive: rangeEndExclusive,
                Revenue: 0m,
                PreviousRevenue: null,
                RevenueChangePercent: null,
                CompletedOrderCount: 0,
                PreviousCompletedOrderCount: null,
                OrderCountChangePercent: null,
                TotalActiveProducts: Stock.Select(item => item.ProductId).Distinct().Count(),
                LowStockCount: lowStockItems.Length,
                RevenuePoints: BuildEmptyPoints(period, rangeStart, rangeEndExclusive),
                LowStockItems: lowStockItems,
                RecentOrders: Array.Empty<RecentOrder>());
        }

        if (_scenario == DashboardFixtureScenario.OnePoint)
        {
            var todayRange = DashboardPeriodHelper.GetPeriodRange(DashboardPeriod.Today, now);
            var localNow = DashboardPeriodHelper.ToHoChiMinhTime(now);
            var pointBucket = period == DashboardPeriod.Today
                ? todayRange.Start.AddHours(localNow.Hour)
                : todayRange.Start;
            const decimal amount = 4_290_000m;
            var onlyOrder = new RecentOrder(
                "MOCK-ONE-POINT",
                pointBucket,
                Customers[0],
                "iPhone 15 Pro Max 256GB Titan",
                amount,
                "Hoàn thành");

            return new DashboardSnapshot(
                GeneratedAt: now,
                Period: period,
                RangeStart: rangeStart,
                RangeEndExclusive: rangeEndExclusive,
                Revenue: amount,
                PreviousRevenue: 0m,
                RevenueChangePercent: null,
                CompletedOrderCount: 1,
                PreviousCompletedOrderCount: 0,
                OrderCountChangePercent: null,
                TotalActiveProducts: Stock.Select(item => item.ProductId).Distinct().Count(),
                LowStockCount: lowStockItems.Length,
                RevenuePoints: [new RevenuePoint(pointBucket, amount)],
                LowStockItems: lowStockItems,
                RecentOrders: [onlyOrder]);
        }

        var orders = BuildSyntheticOrders(now);
        var currentOrders = orders.Where(order => order.CreatedAt >= rangeStart && order.CreatedAt < rangeEndExclusive).ToArray();
        var previousOrders = orders.Where(order => order.CreatedAt >= previousStart && order.CreatedAt < previousEndExclusive).ToArray();
        var completedCurrent = currentOrders.Where(order => order.Status == SyntheticOrderStatus.Completed).ToArray();
        var completedPrevious = previousOrders.Where(order => order.Status == SyntheticOrderStatus.Completed).ToArray();
        var revenue = completedCurrent.Sum(order => order.TotalAmount);
        var previousRevenue = completedPrevious.Sum(order => order.TotalAmount);
        var completedOrderCount = completedCurrent.Length;
        var previousOrderCount = completedPrevious.Length;
        var chartPoints = BuildRevenuePoints(period, rangeStart, rangeEndExclusive, completedCurrent);
        var recentOrders = currentOrders
            .OrderByDescending(order => order.CreatedAt)
            .Take(5)
            .Select(order => new RecentOrder(order.OrderCode, order.CreatedAt, order.CustomerName,
                order.ProductSummary, order.TotalAmount,
                order.Status == SyntheticOrderStatus.Completed ? "Hoàn thành" : "Chờ thanh toán"))
            .ToArray();

        return new DashboardSnapshot(
            GeneratedAt: now,
            Period: period,
            RangeStart: rangeStart,
            RangeEndExclusive: rangeEndExclusive,
            Revenue: revenue,
            PreviousRevenue: previousRevenue,
            RevenueChangePercent: CalculateChange(revenue, previousRevenue),
            CompletedOrderCount: completedOrderCount,
            PreviousCompletedOrderCount: previousOrderCount,
            OrderCountChangePercent: CalculateChange(completedOrderCount, previousOrderCount),
            TotalActiveProducts: Stock.Select(item => item.ProductId).Distinct().Count(),
            LowStockCount: lowStockItems.Length,
            RevenuePoints: chartPoints,
            LowStockItems: lowStockItems,
            RecentOrders: recentOrders);
    }

    private static decimal? CalculateChange(decimal current, decimal previous) =>
        previous == 0m ? null : decimal.Round((current - previous) / previous * 100m, 1);

    private static IReadOnlyList<RevenuePoint> BuildRevenuePoints(
        DashboardPeriod period,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEndExclusive,
        IReadOnlyList<SyntheticOrder> eligibleOrders)
    {
        var points = new List<RevenuePoint>();
        var hourly = period == DashboardPeriod.Today;
        var step = hourly ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        for (var bucket = rangeStart; bucket < rangeEndExclusive; bucket += step)
        {
            var next = bucket + step;
            var total = eligibleOrders
                .Where(order => order.CreatedAt >= bucket && order.CreatedAt < next)
                .Sum(order => order.TotalAmount);
            points.Add(new RevenuePoint(bucket, total));
        }

        return points;
    }

    private static IReadOnlyList<RevenuePoint> BuildEmptyPoints(
        DashboardPeriod period,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEndExclusive)
    {
        var step = period == DashboardPeriod.Today ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var points = new List<RevenuePoint>();
        for (var bucket = rangeStart; bucket < rangeEndExclusive; bucket += step)
        {
            points.Add(new RevenuePoint(bucket, 0m));
        }

        return points;
    }

    private static IReadOnlyList<SyntheticOrder> BuildSyntheticOrders(DateTimeOffset now)
    {
        var (todayStart, _) = DashboardPeriodHelper.GetPeriodRange(DashboardPeriod.Today, now);
        var orders = new List<SyntheticOrder>();
        var sequence = 1;
        for (var daysAgo = 75; daysAgo >= 0; daysAgo--)
        {
            var day = todayStart.AddDays(-daysAgo);
            var localDate = TimeZoneInfo.ConvertTime(day, TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh"));
            var count = daysAgo % 4 == 0 ? 3 : 2;
            for (var orderIndex = 0; orderIndex < count; orderIndex++)
            {
                var hour = 9 + orderIndex * 3;
                var createdAt = new DateTimeOffset(localDate.Year, localDate.Month, localDate.Day, hour, 15, 0, localDate.Offset);
                var status = orderIndex == count - 1 && daysAgo % 3 == 0
                    ? SyntheticOrderStatus.Pending
                    : SyntheticOrderStatus.Completed;
                var productIndex = (daysAgo + orderIndex) % Stock.Length;
                orders.Add(new SyntheticOrder(
                    $"MOCK-{sequence++:D5}",
                    createdAt,
                    Customers[(daysAgo + orderIndex) % Customers.Length],
                    $"{Stock[productIndex].ProductName} {Stock[productIndex].VariantName}",
                    1_250_000m + ((daysAgo * 17 + orderIndex * 31) % 900) * 10_000m,
                    status));
            }
        }

        return orders;
    }

    private enum SyntheticOrderStatus { Completed, Pending }

    private sealed record SyntheticOrder(
        string OrderCode,
        DateTimeOffset CreatedAt,
        string CustomerName,
        string ProductSummary,
        decimal TotalAmount,
        SyntheticOrderStatus Status);

    private sealed record StockRecord(
        string ProductId,
        string ProductName,
        string VariantName,
        int Quantity,
        int ReservedQuantity,
        int MinimumStockAlert,
        string Unit);
}
