namespace TechStore.Shared.DTOs;

public class VoucherDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary><c>Percentage</c> or <c>FixedAmount</c>.</summary>
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    /// <summary>Uses held for sales in payment.</summary>
    public int ReservedCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
