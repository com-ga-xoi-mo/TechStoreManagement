namespace TechStore.Shared.DTOs;

/// <summary>
/// Store-wide financial aggregates for an inclusive Vietnam calendar range, as published by the sales report
/// change. The API now serializes the superset <c>TechStore.Api.Analytics.FinancialReportResponse</c> (refund tax,
/// Cash/VietQR channels and <c>cashByShift</c>). This record intentionally stays the earlier subset so tests can
/// prove an older client still reads the response; do not use it as the full wire contract.
/// </summary>
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
