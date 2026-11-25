namespace TechStore.Shared.DTOs;

public class LoyaltySummaryDto
{
    public Guid CustomerId { get; set; }
    public int Points { get; set; }
    public string Tier { get; set; } = string.Empty;
    public int TierDiscountPercent { get; set; }
    /// <summary>Next tier name, or null at the top tier.</summary>
    public string? NextTier { get; set; }
    /// <summary>Points still missing for the next tier, or null at the top tier.</summary>
    public int? PointsToNextTier { get; set; }
}
