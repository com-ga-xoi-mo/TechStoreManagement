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
    Preview,
    Live
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
    string? VariantName,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int MinStockAlert);

public sealed record RecentOrder(
    string OrderCode,
    string? CustomerName,
    string ProductSummary,
    decimal Total,
    Shared.Enums.OrderStatus Status,
    DateTimeOffset CreatedAt);
