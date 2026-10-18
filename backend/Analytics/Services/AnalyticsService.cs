using System.Data;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics.Services;

public sealed class AnalyticsService(IAnalyticsRepository repository, IUnitOfWork unitOfWork) : IAnalyticsService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<AdminDashboardDto> GetDashboardAsync(string period, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var asOfUtc = (await repository.GetSnapshotTimeUtcAsync(cancellationToken)).ToUniversalTime();
            var localAsOf = new DateTimeOffset(asOfUtc).ToOffset(VietnamOffset);
            var endDate = DateOnly.FromDateTime(localAsOf.DateTime);
            var startDate = period switch
            {
                "today" => endDate,
                "last7Days" => endDate.AddDays(-6),
                "thisMonth" => new DateOnly(endDate.Year, endDate.Month, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported dashboard period.")
            };
            var fromUtc = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;

            var readModel = await repository.GetDashboardAsync(
                new DashboardAnalyticsCriteria(period, fromUtc, asOfUtc), cancellationToken);

            var dailyRows = readModel.DailyRevenue.ToDictionary(row => row.Date, row => row.Revenue);
            var dailyRevenue = new List<AdminDashboardDailyRevenueDto>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
                dailyRevenue.Add(new AdminDashboardDailyRevenueDto(date, dailyRows.GetValueOrDefault(date)));

            var response = new AdminDashboardDto(
                period,
                startDate,
                endDate,
                "UTC+07:00",
                DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc),
                new AdminDashboardSummaryDto(
                    readModel.Revenue,
                    readModel.OrderCount,
                    readModel.ActiveProductCount,
                    readModel.LowStockCount,
                    null,
                    null),
                dailyRevenue,
                readModel.LowStockItems.Select(item => new AdminDashboardLowStockDto(
                    item.Id, item.ProductId, item.ProductName, item.VariantId, item.VariantName,
                    item.Quantity, item.ReservedQuantity, item.Quantity - item.ReservedQuantity,
                    item.MinStockAlert)).ToList(),
                readModel.RecentOrders.Select(order => new AdminDashboardRecentOrderDto(
                    order.Id,
                    order.OrderCode,
                    order.CustomerName,
                    FormatProductSummary(order),
                    order.TotalAmount,
                    order.Status,
                    DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc))).ToList());

            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static string FormatProductSummary(DashboardRecentOrderRow order)
    {
        if (order.ItemLineCount == 0 || string.IsNullOrWhiteSpace(order.FirstProductName))
            return "Không có mặt hàng";

        var firstItem = string.IsNullOrWhiteSpace(order.FirstVariantName)
            ? order.FirstProductName
            : $"{order.FirstProductName} - {order.FirstVariantName}";
        return order.ItemLineCount > 1 ? $"{firstItem} + {order.ItemLineCount - 1} mặt hàng" : firstItem;
    }
}
