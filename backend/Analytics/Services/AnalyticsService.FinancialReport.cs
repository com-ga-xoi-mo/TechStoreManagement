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

    private static FinancialReportResponse BuildFinancialReport(
        FinancialReportQuery query, DateTime asOfUtc, FinancialReportReadModel data)
    {
        var salesByDate = data.SalesByDay.ToDictionary(row => row.Date);
        var receiptsByDate = data.ReceiptsByDay.ToDictionary(row => row.Date);
        var afterSale = data.AfterSale;
        var refundsByDate = afterSale.Refunds.ToDictionary(row => row.Date);
        var restocksByDate = afterSale.Restocks.ToDictionary(row => row.Date);
        var warrantyByDate = afterSale.Warranty.ToDictionary(row => row.Date);
        var qrByDate = afterSale.QrReceipts.ToDictionary(row => row.Date);
        var cashByDate = afterSale.Cash.ToLookup(row => row.Date);
        var daily = new List<FinancialReportDayResponse>();
        for (var date = query.From; date <= query.To; date = date.AddDays(1))
        {
            var sales = salesByDate.GetValueOrDefault(date);
            var receipts = receiptsByDate.GetValueOrDefault(date)?.PaymentReceivedAmount ?? 0m;
            var refunds = refundsByDate.GetValueOrDefault(date);
            var restock = restocksByDate.GetValueOrDefault(date);
            var warranty = warrantyByDate.GetValueOrDefault(date);
            var qr = qrByDate.GetValueOrDefault(date);
            var cash = cashByDate[date];
            daily.Add(new(date, new(sales?.CompletedOrderCount ?? 0, sales?.Quantity ?? 0,
                sales?.AllocationMismatchOrderCount ?? 0, sales?.SalesExcludingTax ?? 0m, sales?.SalesTax ?? 0m, receipts,
                sales is null ? 0m : sales.CostOfGoodsSold,
                refunds?.RefundPaidAmount ?? 0m, refunds is null ? 0m : refunds.RefundedSalesExcludingTax,
                restock is null ? 0m : restock.RestockedCost, warranty is null ? 0m : warranty.WarrantyCost,
                refunds is null ? 0m : refunds.RefundTaxAmount,
                cash.Sum(row => row.CashReceivedAmount), SumKnown(cash, row => row.CashRefundPaidAmount),
                qr is null ? 0m : qr.VietQrReceivedAmount, refunds is null ? 0m : refunds.VietQrRefundPaidAmount)));
        }

        var totals = daily.Select(day => day.Totals).ToArray();
        var summary = new FinancialReportTotalsResponse(
            totals.Sum(day => day.CompletedOrderCount), totals.Sum(day => day.Quantity),
            totals.Sum(day => day.AllocationMismatchOrderCount), totals.Sum(day => day.SalesExcludingTax),
            totals.Sum(day => day.SalesTax), totals.Sum(day => day.PaymentReceivedAmount),
            SumKnown(totals, day => day.CostOfGoodsSold), SumKnown(totals, day => day.RefundPaidAmount),
            SumKnown(totals, day => day.RefundedSalesExcludingTax), SumKnown(totals, day => day.RestockedCost),
            SumKnown(totals, day => day.WarrantyCost), SumKnown(totals, day => day.RefundTaxAmount),
            SumKnown(totals, day => day.CashReceivedAmount), SumKnown(totals, day => day.CashRefundPaidAmount),
            SumKnown(totals, day => day.VietQrReceivedAmount), SumKnown(totals, day => day.VietQrRefundPaidAmount));

        var cashByShift = afterSale.Cash.Where(row => row.PosSessionId.HasValue)
            .GroupBy(row => row.PosSessionId!.Value).OrderBy(group => group.Key)
            .Select(group => new FinancialReportCashShiftResponse(group.Key,
                group.Sum(row => row.CashReceivedAmount), SumKnown(group, row => row.CashRefundPaidAmount)))
            .ToList();

        return new("custom", query.From, query.To, "UTC+07:00", asOfUtc, summary, daily,
            data.Products.Where(row => row.QuantityRank <= FinancialReportReadModel.ProductRankingLimit)
                .OrderBy(row => row.QuantityRank).Select(MapProduct).ToList(),
            data.Products.Where(row => row.RevenueRank <= FinancialReportReadModel.ProductRankingLimit)
                .OrderBy(row => row.RevenueRank).Select(MapProduct).ToList(),
            data.Cashiers.Select(row => new FinancialReportCashierDto(row.CashierUserId, row.CashierName,
                row.CompletedOrderCount, row.Quantity, row.SalesExcludingTax, row.CostOfGoodsSold,
                row.SalesExcludingTax - row.CostOfGoodsSold)).ToList(),
            cashByShift);
    }

    private static decimal? SumKnown<T>(IEnumerable<T> rows, Func<T, decimal?> select)
    {
        var total = 0m;
        foreach (var row in rows)
        {
            if (select(row) is not { } amount) return null;
            total += amount;
        }

        return total;
    }

    private static FinancialReportProductDto MapProduct(FinancialProductRow row)
        => new(row.ProductId, row.ProductName, row.Quantity, row.SalesExcludingTax);
}
