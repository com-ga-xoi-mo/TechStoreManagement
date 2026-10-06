using TechStore.Api.Common.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Entities;

public class VietQrTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? PosSessionId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string BankBin { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string QrContent { get; set; } = string.Empty;
    public VietQrStatus Status { get; set; } = VietQrStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedByUserId { get; set; }

    // Navigation properties
    public virtual Order Order { get; set; } = null!;
    public virtual PosSession? PosSession { get; set; }
    public virtual User? ConfirmedByUser { get; set; }
}
