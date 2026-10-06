using TechStore.Api.Common.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public int LoyaltyPoints { get; set; } = 0;
    public CustomerTier Tier { get; set; } = CustomerTier.Standard;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<CustomerLoyaltyPoint> LoyaltyPointsHistory { get; set; } = new List<CustomerLoyaltyPoint>();
}
