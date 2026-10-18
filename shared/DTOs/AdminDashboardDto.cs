namespace TechStore.Shared.DTOs;

/// <summary>Admin analytics snapshot returned by GET /api/analytics/dashboard.</summary>
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
