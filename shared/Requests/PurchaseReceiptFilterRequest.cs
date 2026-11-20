namespace TechStore.Shared.Requests;

public class PurchaseReceiptFilterRequest
{
    public string? Search { get; set; }
    public Guid? SupplierId { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
