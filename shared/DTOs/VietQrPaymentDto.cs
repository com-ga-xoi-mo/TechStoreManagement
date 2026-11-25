namespace TechStore.Shared.DTOs;

/// <summary>A VietQR payment request. <see cref="QrImagePng"/> is only filled when the request is created.</summary>
public class VietQrPaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string BankBin { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string? BankAccountName { get; set; }
    public string QrContent { get; set; } = string.Empty;
    public string? QrImagePng { get; set; }
    /// <summary>The same code as rows of 1 (dark) and 0 (light) joined by line feeds, so a client can draw it without decoding an image.</summary>
    public string? QrModules { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int SecondsLeft { get; set; }
    public Guid? PosSessionId { get; set; }
    public Guid? CashierUserId { get; set; }
    public string? CashierName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
