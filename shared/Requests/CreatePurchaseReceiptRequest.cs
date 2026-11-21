namespace TechStore.Shared.Requests;

public class CreatePurchaseReceiptRequest
{
    public bool ReceiveLater { get; set; }
    public Guid? ClientRequestId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierDocumentNumber { get; set; }
    public string? Note { get; set; }
    public List<PurchaseReceiptLineRequest>? Lines { get; set; }
}
