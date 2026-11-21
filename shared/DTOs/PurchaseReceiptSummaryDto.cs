using TechStore.Shared.Enums;

namespace TechStore.Shared.DTOs;

public class PurchaseReceiptSummaryDto
{
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Received;
    public Guid Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierDocumentNumber { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public string? ReceivedByName { get; set; }
    public int LineCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
}
