namespace TechStore.Shared.Requests;

/// <summary>Editable voucher fields; the code and discount type are fixed once a voucher exists.</summary>
public class UpdateVoucherRequest
{
    public string? Description { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int? UsageLimit { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
