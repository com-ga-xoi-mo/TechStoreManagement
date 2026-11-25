namespace TechStore.Shared.Requests;

// A VietQR payment request. Prices are never accepted: the server prices every line.
public class CreateVietQrRequest
{
    public Guid? PosSessionId { get; set; }
    public Guid? CheckoutKey { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal? OrderDiscountAmount { get; set; }
    public string? VoucherCode { get; set; }
    public bool? ApplyVipDiscount { get; set; }
    public decimal? ExpectedTotal { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest>? Items { get; set; }
}
