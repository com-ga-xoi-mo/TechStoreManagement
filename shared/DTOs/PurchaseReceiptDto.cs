namespace TechStore.Shared.DTOs;

public class PurchaseReceiptDto
{
    public Guid Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
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
