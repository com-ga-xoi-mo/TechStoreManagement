namespace TechStore.Shared.DTOs;

public class VietQrBankEventDto
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime TransferredAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public Guid? VietQrTransactionId { get; set; }
    public string? OrderCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
