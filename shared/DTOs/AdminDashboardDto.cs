namespace TechStore.Shared.DTOs;

/// <summary>Store-wide analytics snapshot returned to Admin and Manager by GET /api/analytics/dashboard.</summary>
/// <param name="Period">today, last7Days, thisMonth, or custom.</param>
/// <param name="StartDate">Inclusive first Vietnam calendar date of the report.</param>
/// <param name="EndDate">Inclusive last Vietnam calendar date of the report.</param>
/// <param name="TimeZone">Vietnam calendar offset (UTC+07:00).</param>
/// <param name="AsOf">UTC time of the database snapshot, independent of the last calendar date in a historical report.</param>
/// <param name="Summary">Period and snapshot KPI totals.</param>
/// <param name="DailyRevenue">One revenue point for each date in the report.</param>
/// <param name="LowStockItems">Current low-stock balances at the snapshot time.</param>
/// <param name="RecentOrders">Newest orders created within the report period.</param>
public sealed record AdminDashboardDto(
    string Period,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    DateTime AsOf,
    AdminDashboardSummaryDto Summary,
    IReadOnlyList<AdminDashboardDailyRevenueDto> DailyRevenue,
    IReadOnlyList<AdminDashboardLowStockDto> LowStockItems,
    IReadOnlyList<AdminDashboardRecentOrderDto> RecentOrders);

/// <summary>Point-in-time KPI values; comparison values are null until comparison periods are defined.</summary>
public sealed record AdminDashboardSummaryDto(
    decimal Revenue,
    int OrderCount,
    int ActiveProductCount,
    int LowStockCount,
    decimal? RevenueChangePercent,
    decimal? OrderCountChangePercent);

/// <summary>Completed gross revenue for one Vietnam calendar date.</summary>
public sealed record AdminDashboardDailyRevenueDto(DateOnly Date, decimal Revenue);

/// <summary>One inventory balance row meeting the low-stock threshold.</summary>
public sealed record AdminDashboardLowStockDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid? VariantId,
    string? VariantName,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int MinStockAlert);

/// <summary>One real order in the requested period, ordered newest first.</summary>
public sealed record AdminDashboardRecentOrderDto(
    Guid Id,
    string OrderCode,
    string? CustomerName,
    string ProductSummary,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt);
