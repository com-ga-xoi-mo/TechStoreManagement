using TechStore.Shared.Enums;

namespace TechStore.Shared.DTOs;

public class PurchaseReceiptDto
{
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Received;
    public Guid Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierDocumentNumber { get; set; }
    public Guid? ClientRequestId { get; set; }
    public string? Note { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public string? ReceivedByName { get; set; }
    public decimal TotalCost { get; set; }
    public IReadOnlyList<PurchaseReceiptLineDto> Lines { get; set; } = [];
}
