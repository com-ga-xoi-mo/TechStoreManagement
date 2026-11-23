namespace TechStore.Shared.Requests;

public class CreateOrderRequest
{
    public Guid? CustomerId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    // Order-level discount in whole đồng, applied after line discounts and before VAT. Optional, default 0.
    public decimal? OrderDiscountAmount { get; set; }
    // An order uses at most one order-level discount: this voucher code, the customer's VIP discount, or the manual amount above.
    public string? VoucherCode { get; set; }
    public bool? ApplyVipDiscount { get; set; }
    public List<CreateOrderItemRequest>? Items { get; set; }
}
