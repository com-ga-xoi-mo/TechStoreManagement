using TechStore.Client.Models.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Client.Repositories.Analytics;

/// <summary>Reads the server-computed Admin dashboard snapshot and maps it to client presentation models.</summary>
public sealed class AnalyticsApiRepository(IApiClient apiClient) : IAnalyticsRepository
{
    private const string TimeZone = "UTC+07:00";

    public async Task<DashboardReadResult> GetDashboardAsync(
        DashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var periodKey = query.Period switch
        {
            DashboardPeriod.Today => "today",
            DashboardPeriod.Last7Days => "last7Days",
            DashboardPeriod.ThisMonth => "thisMonth",
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Period, "Unsupported dashboard period.")
        };

        var dto = await apiClient.GetAsync<AdminDashboardDto>(
            $"api/analytics/dashboard?period={periodKey}", cancellationToken);
        return DashboardReadResult.Available(Map(dto, query.Period, periodKey));
    }

    private static DashboardSnapshot Map(AdminDashboardDto dto, DashboardPeriod requestedPeriod, string requestedPeriodKey)
    {
        if (dto is null) throw InvalidResponse("Dashboard response is null.");
        if (!string.Equals(dto.Period, requestedPeriodKey, StringComparison.Ordinal))
            throw InvalidResponse("Dashboard response period does not match the request.");
        if (!string.Equals(dto.TimeZone, TimeZone, StringComparison.Ordinal))
            throw InvalidResponse("Dashboard response has an unsupported time zone.");
        if (dto.StartDate > dto.EndDate)
            throw InvalidResponse("Dashboard response has an invalid date range.");
        if (dto.AsOf.Kind != DateTimeKind.Utc)
            throw InvalidResponse("Dashboard as-of timestamp must be UTC.");

        var summary = dto.Summary ?? throw InvalidResponse("Dashboard summary is missing.");
        if (summary.Revenue < 0 || summary.OrderCount < 0 || summary.ActiveProductCount < 0 || summary.LowStockCount < 0)
            throw InvalidResponse("Dashboard summary contains a negative value.");

        var dailyRows = dto.DailyRevenue ?? throw InvalidResponse("Dashboard daily revenue is missing.");
        var expectedDays = dto.EndDate.DayNumber - dto.StartDate.DayNumber + 1;
        if (dailyRows.Count != expectedDays)
            throw InvalidResponse("Dashboard daily revenue does not cover its full date range.");

        var dailyRevenue = new List<DailyRevenuePoint>(dailyRows.Count);
        decimal dailyTotal = 0;
        for (var index = 0; index < dailyRows.Count; index++)
        {
            var row = dailyRows[index];
            var expectedDate = dto.StartDate.AddDays(index);
            if (row.Date != expectedDate || row.Revenue < 0)
                throw InvalidResponse("Dashboard daily revenue contains a missing date or invalid amount.");
            dailyTotal += row.Revenue;
            dailyRevenue.Add(new DailyRevenuePoint(row.Date, row.Revenue));
        }
        if (dailyTotal != summary.Revenue)
            throw InvalidResponse("Dashboard daily revenue does not reconcile with summary revenue.");

        var lowStockRows = dto.LowStockItems ?? throw InvalidResponse("Dashboard low-stock list is missing.");
        if (lowStockRows.Count > 5)
            throw InvalidResponse("Dashboard returned more than five low-stock rows.");
        var lowStock = lowStockRows.Select(row =>
        {
            if (string.IsNullOrWhiteSpace(row.ProductName) || row.Quantity < 0 || row.ReservedQuantity < 0
                || row.ReservedQuantity > row.Quantity || row.AvailableQuantity != row.Quantity - row.ReservedQuantity
                || row.MinStockAlert < 0)
                throw InvalidResponse("Dashboard low-stock row is invalid.");
            return new LowStockItem(row.ProductName, row.VariantName, row.Quantity, row.ReservedQuantity,
                row.AvailableQuantity, row.MinStockAlert);
        }).ToArray();

        var recentRows = dto.RecentOrders ?? throw InvalidResponse("Dashboard recent-orders list is missing.");
        if (recentRows.Count > 5)
            throw InvalidResponse("Dashboard returned more than five recent orders.");
        var recentOrders = recentRows.Select(row =>
        {
            if (string.IsNullOrWhiteSpace(row.OrderCode) || string.IsNullOrWhiteSpace(row.ProductSummary)
                || row.TotalAmount < 0 || row.CreatedAt.Kind != DateTimeKind.Utc
                || !Enum.TryParse<OrderStatus>(row.Status, ignoreCase: false, out var status)
                || !Enum.IsDefined(status))
                throw InvalidResponse("Dashboard recent-order row is invalid.");
            return new RecentOrder(row.OrderCode, row.CustomerName, row.ProductSummary,
                row.TotalAmount, status, new DateTimeOffset(row.CreatedAt));
        }).ToArray();

        return new DashboardSnapshot(
            requestedPeriod,
            dto.StartDate,
            dto.EndDate,
            new DateTimeOffset(dto.AsOf),
            DashboardSourceKind.Live,
            new DashboardSummary(summary.Revenue, summary.OrderCount, summary.ActiveProductCount,
                summary.LowStockCount, summary.RevenueChangePercent, summary.OrderCountChangePercent),
            Array.AsReadOnly(dailyRevenue.ToArray()),
            Array.AsReadOnly(lowStock),
            Array.AsReadOnly(recentOrders));
    }

    private static ApiException InvalidResponse(string message) =>
        new(ApiErrorKind.InvalidResponse, message);
}
