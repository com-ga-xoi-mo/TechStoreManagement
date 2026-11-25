namespace TechStore.Api.Analytics.Repositories;

public sealed record FinancialReportCriteria(DateTime FromUtc, DateTime ToExclusiveUtc);

public sealed record FinancialReportReadModel(
    IReadOnlyList<FinancialSalesDayRow> SalesByDay,
    IReadOnlyList<FinancialReceiptsDayRow> ReceiptsByDay,
    IReadOnlyList<FinancialProductRow> Products,
    IReadOnlyList<FinancialCashierRow> Cashiers,
    FinancialAfterSaleReadModel AfterSale)
{
    internal const int ProductRankingLimit = 10;
}

public sealed record FinancialSalesDayRow(
    DateOnly Date,
    long CompletedOrderCount,
    long Quantity,
    long AllocationMismatchOrderCount,
    decimal SalesExcludingTax,
    decimal SalesTax,
    decimal? CostOfGoodsSold);

public sealed record FinancialReceiptsDayRow(DateOnly Date, decimal PaymentReceivedAmount);

public sealed record FinancialProductRow(Guid ProductId, string ProductName, long Quantity,
    decimal SalesExcludingTax, long QuantityRank, long RevenueRank);

public sealed record FinancialCashierRow(Guid CashierUserId, string CashierName, long CompletedOrderCount,
    long Quantity, decimal SalesExcludingTax, decimal? CostOfGoodsSold);

public sealed record FinancialAfterSaleReadModel(
    IReadOnlyList<FinancialRefundDayRow> Refunds,
    IReadOnlyList<FinancialRestockDayRow> Restocks,
    IReadOnlyList<FinancialWarrantyDayRow> Warranty,
    IReadOnlyList<FinancialCashDayRow> Cash,
    IReadOnlyList<FinancialQrReceiptDayRow> QrReceipts);

public sealed record FinancialRefundDayRow(DateOnly Date, decimal RefundPaidAmount, decimal? RefundTaxAmount,
    decimal? RefundedSalesExcludingTax, decimal? VietQrRefundPaidAmount);

public sealed record FinancialRestockDayRow(DateOnly Date, decimal? RestockedCost);

public sealed record FinancialWarrantyDayRow(DateOnly Date, decimal? WarrantyCost);

public sealed record FinancialCashDayRow(DateOnly Date, Guid? PosSessionId,
    decimal CashReceivedAmount, decimal? CashRefundPaidAmount);

public sealed record FinancialQrReceiptDayRow(DateOnly Date, decimal? VietQrReceivedAmount);
