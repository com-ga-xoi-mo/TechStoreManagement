using TechStore.Api.Common.Entities;

namespace TechStore.Api.Customers.Entities;

public class CustomerLoyaltyPoint : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public bool IsSaleAccrual { get; set; }
    public Guid? RefundCaseId { get; set; }
    public int PointsChange { get; set; }
    public int BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Idempotency key such as <c>earn:{orderId}</c> or <c>reverse:{returnId}</c>; unique when set.</summary>
    public string? ReferenceKey { get; set; }

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
}
