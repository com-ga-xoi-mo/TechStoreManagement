namespace TechStore.Shared.Requests;

public class InventoryStockFilterRequest
{
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public bool? LowStock { get; set; }
    public bool? OutOfStock { get; set; }
    public bool? InStock { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
