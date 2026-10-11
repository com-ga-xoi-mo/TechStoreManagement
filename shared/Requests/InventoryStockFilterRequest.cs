namespace TechStore.Shared.Requests;

public class InventoryStockFilterRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public bool? LowStock { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
