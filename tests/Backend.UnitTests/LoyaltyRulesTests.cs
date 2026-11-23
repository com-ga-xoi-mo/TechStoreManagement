using FluentAssertions;
using TechStore.Api.Customers.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

// M4 Vy: 1 point per 100,000 đồng of goods after discounts and before VAT; VIP tiers by points.
public class LoyaltyRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(99_999, 0)]
    [InlineData(100_000, 1)]
    [InlineData(199_999, 1)]
    [InlineData(1_800_000, 18)]
    [InlineData(900_000, 9)]
    public void PointsFor_GoodsAmount_RoundsDownToWholePoints(decimal goodsAmount, int expected)
    {
        // Act
        var points = LoyaltyRules.PointsFor(goodsAmount);

        // Assert
        points.Should().Be(expected);
    }

    [Fact]
    public void PointsFor_NegativeAmount_ReturnsZero()
    {
        // Act
        var points = LoyaltyRules.PointsFor(-500_000m);

        // Assert
        points.Should().Be(0);
    }

    [Theory]
    [InlineData(0, CustomerTier.Standard)]
    [InlineData(499, CustomerTier.Standard)]
    [InlineData(500, CustomerTier.Silver)]
    [InlineData(1_499, CustomerTier.Silver)]
    [InlineData(1_500, CustomerTier.Gold)]
    [InlineData(4_999, CustomerTier.Gold)]
    [InlineData(5_000, CustomerTier.Platinum)]
    [InlineData(120_000, CustomerTier.Platinum)]
    public void TierFor_Points_UsesTheTierThresholds(int points, CustomerTier expected)
    {
        // Act
        var tier = LoyaltyRules.TierFor(points);

        // Assert
        tier.Should().Be(expected);
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 0)]
    [InlineData(CustomerTier.Silver, 2)]
    [InlineData(CustomerTier.Gold, 5)]
    [InlineData(CustomerTier.Platinum, 10)]
    public void DiscountPercentFor_Tier_ReturnsTheTierDiscount(CustomerTier tier, int expected)
    {
        // Act
        var percent = LoyaltyRules.DiscountPercentFor(tier);

        // Assert
        percent.Should().Be(expected);
    }

    [Theory]
    [InlineData(CustomerTier.Standard, CustomerTier.Silver, 500)]
    [InlineData(CustomerTier.Silver, CustomerTier.Gold, 1_500)]
    [InlineData(CustomerTier.Gold, CustomerTier.Platinum, 5_000)]
    public void NextTier_BelowPlatinum_ReturnsTheNextTierAndItsThreshold(CustomerTier tier, CustomerTier expectedNext, int expectedThreshold)
    {
        // Act
        var next = LoyaltyRules.NextTier(tier);

        // Assert
        next.Should().NotBeNull();
        next!.Value.Tier.Should().Be(expectedNext);
        next.Value.RequiredPoints.Should().Be(expectedThreshold);
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 2_000_000, 0)]
    [InlineData(CustomerTier.Silver, 2_000_000, 40_000)]
    [InlineData(CustomerTier.Gold, 2_000_000, 100_000)]
    [InlineData(CustomerTier.Platinum, 10_000_000, 1_000_000)]
    [InlineData(CustomerTier.Platinum, 1_234_567, 123_457)]
    public void VipDiscount_TierAndGoods_ReturnsWholeDongDiscount(CustomerTier tier, decimal goodsAmount, decimal expected)
    {
        // Act
        var discount = LoyaltyRules.VipDiscount(goodsAmount, tier);

        // Assert
        discount.Should().Be(expected);
    }

    [Fact]
    public void NextTier_Platinum_ReturnsNull()
    {
        // Act
        var next = LoyaltyRules.NextTier(CustomerTier.Platinum);

        // Assert
        next.Should().BeNull();
    }
}
