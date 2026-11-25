using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics;

/// <summary>The financial report wire contract, including after-sale and shift aggregates.</summary>
public sealed record FinancialReportResponse(
    string Period,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    DateTime AsOf,
    FinancialReportTotalsResponse Summary,
    IReadOnlyList<FinancialReportDayResponse> Daily,
    IReadOnlyList<FinancialReportProductDto> TopProductsByQuantity,
    IReadOnlyList<FinancialReportProductDto> TopProductsByRevenue,
    IReadOnlyList<FinancialReportCashierDto> CashierPerformance,
    IReadOnlyList<FinancialReportCashShiftResponse> CashByShift);

public sealed record FinancialReportDayResponse(DateOnly Date, FinancialReportTotalsResponse Totals);

/// <summary>VND amounts. Null means the relevant input or a required allocation is unavailable.</summary>
public sealed record FinancialReportTotalsResponse(
    long CompletedOrderCount,
    long Quantity,
    long AllocationMismatchOrderCount,
    decimal SalesExcludingTax,
    decimal SalesTax,
    decimal PaymentReceivedAmount,
    decimal? CostOfGoodsSold,
    decimal? RefundPaidAmount,
    decimal? RefundedSalesExcludingTax,
    decimal? RestockedCost,
    decimal? WarrantyCost,
    decimal? RefundTaxAmount,
    decimal? CashReceivedAmount,
    decimal? CashRefundPaidAmount,
    decimal? VietQrReceivedAmount,
    decimal? VietQrRefundPaidAmount)
{
    public decimal? SalesGrossProfit => SalesExcludingTax - CostOfGoodsSold;
    public decimal? NetSalesExcludingTax => SalesExcludingTax - RefundedSalesExcludingTax;
    public decimal? NetCostOfGoodsSold => CostOfGoodsSold - RestockedCost;
    public decimal? NetGrossProfit => NetSalesExcludingTax - NetCostOfGoodsSold;
}

public sealed record FinancialReportCashShiftResponse(Guid PosSessionId, decimal? CashReceivedAmount, decimal? CashRefundPaidAmount)
{
    public decimal? CashNetMovement => CashReceivedAmount - CashRefundPaidAmount;
}
