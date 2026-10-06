using TechStore.Api.Common.Entities;

namespace TechStore.Api.Customers.Entities;

public class CustomerLoyaltyPoint : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public int PointsChange { get; set; }
    public int BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
}
