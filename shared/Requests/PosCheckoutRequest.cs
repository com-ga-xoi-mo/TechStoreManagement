namespace TechStore.Shared.Requests;

// Cash checkout. Prices are never accepted: the server prices every line.
public class PosCheckoutRequest
{
    public Guid? PosSessionId { get; set; }
    public Guid? CheckoutKey { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal? OrderDiscountAmount { get; set; }
    public decimal? ExpectedTotal { get; set; }
    public decimal? CashTendered { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest>? Items { get; set; }
}
