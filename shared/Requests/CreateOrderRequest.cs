namespace TechStore.Shared.Requests;

public class CreateOrderRequest
{
    public Guid? CustomerId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    // Order-level discount in whole đồng, applied after line discounts and before VAT. Optional, default 0.
    public decimal? OrderDiscountAmount { get; set; }
    public List<CreateOrderItemRequest>? Items { get; set; }
}
