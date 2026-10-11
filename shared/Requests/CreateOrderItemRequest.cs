namespace TechStore.Shared.Requests;

public class CreateOrderItemRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? SerialImeiId { get; set; }
    public int? Quantity { get; set; }
    public decimal? DiscountAmount { get; set; }
}
