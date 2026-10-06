using TechStore.Api.Common.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Entities;

public class Voucher : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VoucherType DiscountType { get; set; } = VoucherType.Percentage;
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; } = 0;
    public decimal? MaxDiscountAmount { get; set; }
    public int UsageLimit { get; set; } = 100;
    public int UsedCount { get; set; } = 0;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
