namespace TechStore.Shared.Requests;

public class PurchaseReceiptLineRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public int? WarrantyMonths { get; set; }
    public List<PurchaseReceiptDeviceRequest>? Devices { get; set; }
}
