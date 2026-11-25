namespace TechStore.Shared.Requests;

public class CreateVoucherRequest
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    /// <summary><c>Percentage</c> or <c>FixedAmount</c>.</summary>
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int? UsageLimit { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
