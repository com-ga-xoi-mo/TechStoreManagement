namespace TechStore.Shared.DTOs;

/// <summary>Store-wide financial aggregates for an inclusive Vietnam calendar range.</summary>
public sealed record FinancialReportDto(
    string Period,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    DateTime AsOf,
    FinancialReportTotalsDto Summary,
    IReadOnlyList<FinancialReportDayDto> Daily,
    IReadOnlyList<FinancialReportProductDto> TopProductsByQuantity,
    IReadOnlyList<FinancialReportProductDto> TopProductsByRevenue,
    IReadOnlyList<FinancialReportCashierDto> CashierPerformance);

/// <summary>Amounts in VND. Null denotes an unavailable value. Sales and receipts have separate timestamps.</summary>
public sealed record FinancialReportTotalsDto(
    long CompletedOrderCount,
    long Quantity,
    long AllocationMismatchOrderCount,
    decimal SalesExcludingTax,
    decimal SalesTax,
    decimal PaymentReceivedAmount,
    decimal? CostOfGoodsSold,
    decimal? SalesGrossProfit,
    decimal? RefundPaidAmount = null,
    decimal? RefundedSalesExcludingTax = null,
    decimal? RestockedCost = null,
    decimal? WarrantyCost = null,
    decimal? NetSalesExcludingTax = null,
    decimal? NetCostOfGoodsSold = null,
    decimal? NetGrossProfit = null);

public sealed record FinancialReportDayDto(DateOnly Date, FinancialReportTotalsDto Totals);

public sealed record FinancialReportProductDto(Guid ProductId, string ProductName, long Quantity, decimal SalesExcludingTax);

public sealed record FinancialReportCashierDto(
    Guid CashierUserId,
    string CashierName,
    long CompletedOrderCount,
    long Quantity,
    decimal SalesExcludingTax,
    decimal? CostOfGoodsSold,
    decimal? SalesGrossProfit);
