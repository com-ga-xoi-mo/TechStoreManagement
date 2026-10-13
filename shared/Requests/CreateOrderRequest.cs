namespace TechStore.Shared.Requests;

public class CreateOrderRequest
{
    public Guid? CustomerId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest>? Items { get; set; }
}
