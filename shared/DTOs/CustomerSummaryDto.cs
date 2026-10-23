namespace TechStore.Shared.DTOs;

public class CustomerSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int LoyaltyPoints { get; set; }
    public string Tier { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastPurchaseAt { get; set; }
}
