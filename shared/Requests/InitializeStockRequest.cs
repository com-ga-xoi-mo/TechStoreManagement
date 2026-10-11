namespace TechStore.Shared.Requests;

public class InitializeStockRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int? Quantity { get; set; }
    public int? MinStockAlert { get; set; }
    public string? Reason { get; set; }
}
