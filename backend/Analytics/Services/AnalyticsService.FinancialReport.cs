using System.Data;
using TechStore.Api.Analytics.Repositories;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics.Services;

public sealed partial class AnalyticsService
{
    public async Task<FinancialReportResult> GetFinancialReportAsync(
        FinancialReportQuery query, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var asOfUtc = (await repository.GetSnapshotTimeUtcAsync(cancellationToken)).ToUniversalTime();
            var today = DateOnly.FromDateTime(new DateTimeOffset(asOfUtc).ToOffset(VietnamOffset).DateTime);
            if (ValidateRange(query.From, query.To, today) is { } error)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return FinancialReportResult.Invalid(error.Field, error.Message);
            }

            var endExclusive = query.To == today ? asOfUtc : VietnamMidnightUtc(query.To.AddDays(1));
            var data = await repository.GetFinancialReportAsync(new(VietnamMidnightUtc(query.From), endExclusive), cancellationToken);
            var report = BuildFinancialReport(query, asOfUtc, data);
            await unitOfWork.CommitAsync(cancellationToken);
            return FinancialReportResult.Success(report);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static FinancialReportDto BuildFinancialReport(
        FinancialReportQuery query, DateTime asOfUtc, FinancialReportReadModel data)
    {
        var salesByDate = data.SalesByDay.ToDictionary(row => row.Date);
        var receiptsByDate = data.ReceiptsByDay.ToDictionary(row => row.Date);
        var daily = new List<FinancialReportDayDto>();
        for (var date = query.From; date <= query.To; date = date.AddDays(1))
        {
            var sales = salesByDate.GetValueOrDefault(date);
            var revenue = sales?.SalesExcludingTax ?? 0m;
            var cost = sales is null ? 0m : sales.CostOfGoodsSold;
            var receipts = receiptsByDate.GetValueOrDefault(date)?.PaymentReceivedAmount ?? 0m;
            daily.Add(new(date, new(sales?.CompletedOrderCount ?? 0, sales?.Quantity ?? 0,
                sales?.AllocationMismatchOrderCount ?? 0, revenue, sales?.SalesTax ?? 0m, receipts,
                cost, revenue - cost)));
        }

        var totals = daily.Select(day => day.Totals).ToArray();
        var totalSales = totals.Sum(day => day.SalesExcludingTax);
        var totalCost = totals.Any(day => day.CostOfGoodsSold is null) ? null : totals.Sum(day => day.CostOfGoodsSold);
        var summary = new FinancialReportTotalsDto(totals.Sum(day => day.CompletedOrderCount), totals.Sum(day => day.Quantity),
            totals.Sum(day => day.AllocationMismatchOrderCount), totalSales, totals.Sum(day => day.SalesTax),
            totals.Sum(day => day.PaymentReceivedAmount), totalCost, totalSales - totalCost);

        return new("custom", query.From, query.To, "UTC+07:00", asOfUtc, summary, daily,
            data.Products.Where(row => row.QuantityRank <= FinancialReportReadModel.ProductRankingLimit)
                .OrderBy(row => row.QuantityRank).Select(MapProduct).ToList(),
            data.Products.Where(row => row.RevenueRank <= FinancialReportReadModel.ProductRankingLimit)
                .OrderBy(row => row.RevenueRank).Select(MapProduct).ToList(),
            data.Cashiers.Select(row => new FinancialReportCashierDto(row.CashierUserId, row.CashierName,
                row.CompletedOrderCount, row.Quantity, row.SalesExcludingTax, row.CostOfGoodsSold,
                row.SalesExcludingTax - row.CostOfGoodsSold)).ToList());
    }

    private static FinancialReportProductDto MapProduct(FinancialProductRow row)
        => new(row.ProductId, row.ProductName, row.Quantity, row.SalesExcludingTax);
}
