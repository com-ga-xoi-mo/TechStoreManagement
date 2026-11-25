using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Sales;

/// <summary>A VietQR payment request. <see cref="QrImagePng"/> (base64) is only present right after it is created.</summary>
public sealed record VietQrPayment(
    Guid Id,
    Guid OrderId,
    string OrderCode,
    VietQrStatus Status,
    decimal Amount,
    string TransactionCode,
    string BankBin,
    string BankAccountNumber,
    string? BankAccountName,
    string QrContent,
    string? QrImagePng,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? PosSessionId,
    string? CashierName,
    DateTime? ConfirmedAt)
{
    public bool IsPending => Status == VietQrStatus.Pending;
}

/// <summary>Bank money that did not complete a sale by itself, kept for a manager to reconcile.</summary>
public sealed record VietQrBankEvent(
    Guid Id,
    string Provider,
    string ProviderTransactionId,
    decimal Amount,
    string TransferContent,
    string AccountNumber,
    DateTime TransferredAt,
    string? OrderCode,
    VietQrEventStatus Status,
    VietQrEventReason Reason,
    DateTime? ResolvedAt,
    string? ResolutionNote)
{
    public bool NeedsReconciliation => Status == VietQrEventStatus.NeedsReconciliation;
}

public static class VietQrPresentation
{
    public static string StatusText(VietQrStatus status) => status switch
    {
        VietQrStatus.Pending => "Đang chờ thanh toán",
        VietQrStatus.Confirmed => "Đã thanh toán",
        VietQrStatus.Failed => "Đã hủy",
        VietQrStatus.Expired => "Đã hết hạn",
        _ => status.ToString()
    };

    public static string ReasonText(VietQrEventReason reason) => reason switch
    {
        VietQrEventReason.Applied => "Đã hoàn tất đơn",
        VietQrEventReason.LateAfterExpiry => "Tiền đến sau khi mã hết hạn",
        VietQrEventReason.LateAfterCancel => "Tiền đến sau khi hủy mã",
        VietQrEventReason.LateAfterShiftClose => "Tiền đến sau khi đóng ca",
        VietQrEventReason.AmountMismatch => "Sai số tiền",
        VietQrEventReason.AccountMismatch => "Sai tài khoản nhận",
        VietQrEventReason.Unmatched => "Không khớp mã chuyển khoản",
        VietQrEventReason.DuplicateReference => "Mã tham chiếu đã dùng hoặc đơn đã thanh toán",
        VietQrEventReason.SettlementFailed => "Khớp mã nhưng không hoàn tất được đơn",
        _ => reason.ToString()
    };

    /// <summary>"12:34" for the cashier's countdown; never negative.</summary>
    public static string Countdown(TimeSpan left) =>
        left <= TimeSpan.Zero ? "00:00" : $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
}
