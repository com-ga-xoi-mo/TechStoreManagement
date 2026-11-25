namespace TechStore.Client.Models.Loyalty;

/// <summary>Points, tier, the tier's discount and the way to the next tier (both null at the top tier).</summary>
public sealed record LoyaltySummary(
    Guid CustomerId,
    int Points,
    string Tier,
    int TierDiscountPercent,
    string? NextTier,
    int? PointsToNextTier);

/// <summary>Tier floors: the same numbers as <c>LoyaltyRules</c> on the server. Only used to draw the progress bar.</summary>
public static class LoyaltyTierThresholds
{
    public static int For(string? tier) => tier switch
    {
        "Silver" => 500,
        "Gold" => 1500,
        "Platinum" => 5000,
        _ => 0
    };

    private static bool IsKnown(string? tier) => tier is "Standard" or "Silver" or "Gold" or "Platinum";

    /// <summary>Share of the way from the current tier's floor to the next tier's floor, between 0 and 1.</summary>
    public static double Progress(LoyaltySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.NextTier is null) return 1.0;
        if (IsKnown(summary.NextTier) && IsKnown(summary.Tier))
        {
            var floor = For(summary.Tier);
            var span = For(summary.NextTier) - floor;
            if (span > 0) return Math.Clamp((summary.Points - floor) / (double)span, 0.0, 1.0);
        }

        // A tier this client does not know yet: use what the server says is still missing.
        var missing = summary.PointsToNextTier ?? 0;
        var total = summary.Points + missing;
        return total <= 0 ? 0.0 : Math.Clamp(summary.Points / (double)total, 0.0, 1.0);
    }
}
