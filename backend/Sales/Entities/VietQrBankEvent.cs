using TechStore.Api.Common.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Entities;

/// <summary>
/// One bank notification of money received, kept even when it cannot complete a sale. The verified payload is
/// immutable evidence; only the review fields change.
/// </summary>
public class VietQrBankEvent : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime TransferredAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public Guid? VietQrTransactionId { get; set; }
    public VietQrEventStatus Status { get; set; }
    public VietQrEventReason Reason { get; set; }
    public string Payload { get; set; } = string.Empty;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public virtual VietQrTransaction? VietQrTransaction { get; set; }
    public virtual User? ResolvedByUser { get; set; }
}
