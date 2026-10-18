namespace TechStore.Api.Analytics.Repositories;

/// <summary>Validated UTC boundaries and period key for one dashboard read.</summary>
public sealed record DashboardAnalyticsCriteria(string Period, DateTime FromUtc, DateTime AsOfUtc);

/// <summary>Compact repository projection; contains no persistence entities or API DTOs.</summary>
public sealed record DashboardAnalyticsReadModel(
    decimal Revenue,
    int OrderCount,
    int ActiveProductCount,
    int LowStockCount,
    IReadOnlyList<DashboardDailyRevenueRow> DailyRevenue,
    IReadOnlyList<DashboardLowStockRow> LowStockItems,
    IReadOnlyList<DashboardRecentOrderRow> RecentOrders);

public sealed record DashboardDailyRevenueRow(DateOnly Date, decimal Revenue);

/// <summary>Mutable projection shape used only to materialize the parameterized PostgreSQL daily aggregate.</summary>
public sealed class DashboardDailyRevenueSqlRow
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
}

public sealed record DashboardLowStockRow(
    Guid Id, Guid ProductId, string ProductName, Guid? VariantId, string? VariantName,
    int Quantity, int ReservedQuantity, int MinStockAlert);

public sealed record DashboardRecentOrderRow(
    Guid Id, string OrderCode, string? CustomerName, string? FirstProductName,
    string? FirstVariantName, int ItemLineCount, decimal TotalAmount,
    string Status, DateTime CreatedAt);
