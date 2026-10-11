namespace TechStore.Shared.Requests;

public class BatchImportSerialImeiRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public List<SerialImeiImportItemRequest>? Items { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public decimal? DefaultUnitCost { get; set; }
    public int? DefaultWarrantyMonths { get; set; }
    public string? Reason { get; set; }
}
