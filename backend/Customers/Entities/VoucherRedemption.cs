using TechStore.Api.Common.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Customers.Entities;

public enum VoucherRedemptionStatus
{
    Reserved,
    Redeemed,
    Released
}

/// <summary>One use of a voucher by one order: held when payment starts, consumed on payment, returned on cancel or expiry.</summary>
public class VoucherRedemption : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Guid OrderId { get; set; }
    public VoucherRedemptionStatus Status { get; set; } = VoucherRedemptionStatus.Reserved;
    public DateTime? RedeemedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
    public virtual Order Order { get; set; } = null!;
}
