namespace TechStore.Client.Models.Analytics;

public sealed record DashboardSnapshot(
    DashboardPeriod Period,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset AsOf,
    DashboardSourceKind SourceKind,
    DashboardSummary Summary,
    IReadOnlyList<DailyRevenuePoint> DailyRevenue,
    IReadOnlyList<LowStockItem> LowStockItems,
    IReadOnlyList<RecentOrder> RecentOrders);

public enum DashboardSourceKind
{
    Demo
}

public sealed record DashboardSummary(
    decimal Revenue,
    int OrderCount,
    int ProductCount,
    int LowStockCount,
    decimal? RevenueChangePercent,
    decimal? OrderCountChangePercent);

public sealed record DailyRevenuePoint(DateOnly Date, decimal Revenue);

public sealed record LowStockItem(
    string ProductName,
    string VariantDescription,
    int AvailableQuantity,
    int ReorderThreshold);

public sealed record RecentOrder(
    string DemoDisplayId,
    string CustomerDisplayName,
    string ProductSummary,
    string? SerialLabel,
    decimal Total,
    Shared.Enums.OrderStatus Status,
    DateTimeOffset CreatedAt);
