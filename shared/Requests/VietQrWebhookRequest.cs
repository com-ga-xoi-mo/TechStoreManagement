namespace TechStore.Shared.Requests;

// Body of the sandbox bank notification. The signature covers the raw body, not this object.
public class VietQrWebhookRequest
{
    public string? TransactionId { get; set; }
    public decimal? Amount { get; set; }
    public string? TransferContent { get; set; }
    public string? AccountNumber { get; set; }
    public DateTime? TransferredAt { get; set; }
}
