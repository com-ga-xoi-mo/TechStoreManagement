using TechStore.Client.Models.Orders;
using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Sales;

/// <summary>A cash shift as the server reports it. <see cref="ExpectedCash"/> is opening cash plus cash sales.</summary>
public sealed record PosShift(
    Guid Id,
    Guid CashierUserId,
    string? CashierName,
    PosSessionStatus Status,
    decimal OpeningBalance,
    decimal CashSalesTotal,
    decimal ExpectedCash,
    decimal? ClosingBalance,
    decimal? Discrepancy,
    string? ClosingNote,
    DateTime OpenedAt,
    DateTime? ClosedAt)
{
    public bool IsOpen => Status == PosSessionStatus.Open;
}

/// <summary>An in-stock device found by scanning its IMEI or serial number, with where it belongs.</summary>
public sealed record ScannedDevice(AvailableDevice Device, Guid ProductId, Guid? VariantId);
