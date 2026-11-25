using System.Globalization;
using System.Text.Json.Serialization;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Client.Repositories.Analytics;

/// <summary>Reads and maps the server-computed store-wide dashboard snapshot.</summary>
public sealed class AnalyticsApiRepository(IApiClient apiClient) : IAnalyticsRepository, IFinancialReportRepository
{
    private const string TimeZone = "UTC+07:00";
    private const int MaxListRows = 5;

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
            DashboardPeriod.Custom => "custom",
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Period, "Unsupported dashboard period.")
        };

        var path = $"api/analytics/dashboard?period={periodKey}";
        if (query.Period == DashboardPeriod.Custom)
        {
            var range = query.Range ?? throw new ArgumentException("A custom dashboard period requires both dates.", nameof(query));
            path = string.Create(CultureInfo.InvariantCulture, $"{path}&from={range.From:yyyy-MM-dd}&to={range.To:yyyy-MM-dd}");
        }
        else if (query.Range is not null)
        {
            throw new ArgumentException("Dashboard dates require a custom period.", nameof(query));
        }

        var dto = await apiClient.GetAsync<AdminDashboardDto>(path, cancellationToken);
        return DashboardReadResult.Available(Map(dto, query, periodKey));
    }

    public async Task<FinancialReportSnapshot> GetFinancialReportAsync(FinancialReportRange range,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/analytics/financial-report?period=custom&from={range.From:yyyy-MM-dd}&to={range.To:yyyy-MM-dd}");
        var report = await apiClient.GetAsync<FinancialReportResponse>(path, cancellationToken);

        if (report is null || report.Period != "custom" || report.TimeZone != TimeZone
            || report.StartDate != range.From || report.EndDate != range.To || range.From > range.To
            || report.AsOf.Kind != DateTimeKind.Utc)
            throw InvalidResponse("Financial report metadata does not match the requested range.");

        var days = range.To.DayNumber - range.From.DayNumber + 1;
        if (report.Summary is null || report.Daily is null || report.Daily.Count != days)
            throw InvalidResponse("Financial report totals are missing.");

        for (var index = 0; index < days; index++)
        {
            var day = report.Daily[index];
            if (day?.Totals is null || day.Date != range.From.AddDays(index))
                throw InvalidResponse("Financial report daily totals do not cover the requested range in order.");
        }

        if (report.TopProductsByQuantity is null || report.TopProductsByRevenue is null
            || report.CashierPerformance is null
            || report.TopProductsByQuantity.Any(row => string.IsNullOrWhiteSpace(row?.ProductName))
            || report.TopProductsByRevenue.Any(row => string.IsNullOrWhiteSpace(row?.ProductName))
            || report.CashierPerformance.Any(row => string.IsNullOrWhiteSpace(row?.CashierName))
            || report.CashByShift?.Any(row => row is null || row.PosSessionId == Guid.Empty) == true)
            throw InvalidResponse("Financial report contains an invalid breakdown.");

        return new FinancialReportSnapshot(range, new DateTimeOffset(report.AsOf), report.Daily, report.Summary,
            report.TopProductsByQuantity, report.TopProductsByRevenue, report.CashierPerformance, report.CashByShift);
    }

    private sealed record FinancialReportResponse(
        [property: JsonRequired] string Period,
        [property: JsonRequired] DateOnly StartDate,
        [property: JsonRequired] DateOnly EndDate,
        [property: JsonRequired] string TimeZone,
        [property: JsonRequired] DateTime AsOf,
        [property: JsonRequired] FinancialReportTotals Summary,
        [property: JsonRequired] IReadOnlyList<FinancialReportDay> Daily,
        [property: JsonRequired] IReadOnlyList<FinancialReportProductDto> TopProductsByQuantity,
        [property: JsonRequired] IReadOnlyList<FinancialReportProductDto> TopProductsByRevenue,
        [property: JsonRequired] IReadOnlyList<FinancialReportCashierDto> CashierPerformance,
        IReadOnlyList<FinancialReportCashShift>? CashByShift = null);

    private static DashboardSnapshot Map(AdminDashboardDto dto, DashboardQuery query, string requestedPeriodKey)
    {
        if (dto is null) throw InvalidResponse("Dashboard response is null.");
        if (!string.Equals(dto.Period, requestedPeriodKey, StringComparison.Ordinal))
            throw InvalidResponse("Dashboard response period does not match the request.");
        if (!string.Equals(dto.TimeZone, TimeZone, StringComparison.Ordinal))
            throw InvalidResponse("Dashboard response has an unsupported time zone.");
        if (dto.StartDate > dto.EndDate)
            throw InvalidResponse("Dashboard response has an invalid date range.");
        if (query.Range is { } range && (dto.StartDate != range.From || dto.EndDate != range.To))
            throw InvalidResponse("Dashboard response dates do not match the requested range.");
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
        if (lowStockRows.Count > MaxListRows)
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
        if (recentRows.Count > MaxListRows)
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
            query.Period,
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
