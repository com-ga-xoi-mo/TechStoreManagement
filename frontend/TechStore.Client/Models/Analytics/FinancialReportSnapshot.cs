using System.Text.Json.Serialization;
using TechStore.Shared.DTOs;

namespace TechStore.Client.Models.Analytics;

public readonly record struct FinancialReportRange(DateOnly From, DateOnly To);

public sealed record FinancialReportSnapshot(
    FinancialReportRange Range,
    DateTimeOffset AsOf,
    IReadOnlyList<FinancialReportDay> Daily,
    FinancialReportTotals Summary,
    IReadOnlyList<FinancialReportProductDto> TopProductsByQuantity,
    IReadOnlyList<FinancialReportProductDto> TopProductsByRevenue,
    IReadOnlyList<FinancialReportCashierDto> CashierPerformance,
    IReadOnlyList<FinancialReportCashShift>? CashByShift);

public sealed record FinancialReportDay(
    [property: JsonRequired] DateOnly Date,
    [property: JsonRequired] FinancialReportTotals Totals);

[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record FinancialReportTotals(
    [property: JsonRequired] long CompletedOrderCount,
    [property: JsonRequired] long Quantity,
    [property: JsonRequired] long AllocationMismatchOrderCount,
    [property: JsonRequired] decimal SalesExcludingTax,
    [property: JsonRequired] decimal SalesTax,
    [property: JsonRequired] decimal PaymentReceivedAmount,
    [property: JsonRequired] decimal? CostOfGoodsSold,
    [property: JsonRequired] decimal? SalesGrossProfit,
    decimal? RefundPaidAmount = null,
    decimal? RefundedSalesExcludingTax = null,
    decimal? RestockedCost = null,
    decimal? WarrantyCost = null,
    decimal? NetSalesExcludingTax = null,
    decimal? NetCostOfGoodsSold = null,
    decimal? NetGrossProfit = null,
    decimal? RefundTaxAmount = null,
    decimal? CashReceivedAmount = null,
    decimal? CashRefundPaidAmount = null,
    decimal? VietQrReceivedAmount = null,
    decimal? VietQrRefundPaidAmount = null);

[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record FinancialReportCashShift(
    [property: JsonRequired] Guid PosSessionId,
    decimal? CashReceivedAmount = null,
    decimal? CashRefundPaidAmount = null,
    decimal? CashNetMovement = null);
