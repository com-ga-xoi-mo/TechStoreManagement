using System;
using System.Collections.Generic;

namespace TechStore.Client.Models.Analytics;

public enum DashboardPeriod
{
    Today,
    Last7Days,
    ThisMonth
}

public enum DashboardDataProvenance
{
    SyntheticPreview
}

public sealed record RevenuePoint(DateTimeOffset BucketStart, decimal Revenue);

public sealed record LowStockItem(
    string ProductId,
    string ProductName,
    string VariantName,
    int AvailableQuantity,
    int MinimumStockAlert,
    string Unit);

public sealed record RecentOrder(
    string OrderCode,
    DateTimeOffset CreatedAt,
    string CustomerName,
    string ProductSummary,
    decimal TotalAmount,
    string Status);

/// <summary>
/// One coherent dashboard response. Revenue and order count describe the selected
/// range; product and low-stock counts describe inventory at GeneratedAt.
/// </summary>
public sealed record DashboardSnapshot(
    DateTimeOffset GeneratedAt,
    DashboardPeriod Period,
    DateTimeOffset RangeStart,
    DateTimeOffset RangeEndExclusive,
    decimal Revenue,
    decimal? PreviousRevenue,
    decimal? RevenueChangePercent,
    int CompletedOrderCount,
    int? PreviousCompletedOrderCount,
    decimal? OrderCountChangePercent,
    int TotalActiveProducts,
    int LowStockCount,
    IReadOnlyList<RevenuePoint> RevenuePoints,
    IReadOnlyList<LowStockItem> LowStockItems,
    IReadOnlyList<RecentOrder> RecentOrders)
{
    public DashboardDataProvenance Provenance { get; init; } = DashboardDataProvenance.SyntheticPreview;
    public bool HasSales => CompletedOrderCount > 0;
    public decimal? AverageOrderValue => CompletedOrderCount == 0 ? null : Revenue / CompletedOrderCount;
}
