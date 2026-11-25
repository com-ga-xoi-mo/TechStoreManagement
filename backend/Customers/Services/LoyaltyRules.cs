using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Services;

/// <summary>The next membership tier and the points a customer needs to reach it.</summary>
public readonly record struct TierStep(CustomerTier Tier, int RequiredPoints);

/// <summary>
/// Points and VIP rules (owner: Vy), pure so callers and tests share one definition. Points come from the goods value
/// after line and order discounts and before VAT; tiers follow the point balance.
/// </summary>
public static class LoyaltyRules
{
    public const decimal AmountPerPoint = 100_000m;

    // Ascending by points; each entry is the tier reached from that many points and its discount percent.
    private static readonly (int MinPoints, CustomerTier Tier, int DiscountPercent)[] Tiers =
    [
        (0, CustomerTier.Standard, 0),
        (500, CustomerTier.Silver, 2),
        (1_500, CustomerTier.Gold, 5),
        (5_000, CustomerTier.Platinum, 10)
    ];

    public static int PointsFor(decimal goodsAmount) =>
        goodsAmount <= 0m ? 0 : (int)decimal.Floor(goodsAmount / AmountPerPoint);

    public static CustomerTier TierFor(int points) =>
        Tiers.Last(t => points >= t.MinPoints).Tier;

    public static int DiscountPercentFor(CustomerTier tier) =>
        Tiers.First(t => t.Tier == tier).DiscountPercent;

    /// <summary>VIP discount on the goods value after line discounts, in whole đồng (half away from zero).</summary>
    public static decimal VipDiscount(decimal goodsAmount, CustomerTier tier) =>
        decimal.Round(goodsAmount * DiscountPercentFor(tier) / 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>The tier above <paramref name="tier"/>, or null at the top.</summary>
    public static TierStep? NextTier(CustomerTier tier)
    {
        var index = Array.FindIndex(Tiers, t => t.Tier == tier);
        return index < 0 || index == Tiers.Length - 1
            ? null
            : new TierStep(Tiers[index + 1].Tier, Tiers[index + 1].MinPoints);
    }
}
