using TechStore.Client.Models.Loyalty;

namespace TechStore.Client.UnitTests.Models.Loyalty;

public sealed class LoyaltyTierThresholdsTests
{
    [Theory]
    [InlineData("Standard", 0)]
    [InlineData("Silver", 500)]
    [InlineData("Gold", 1500)]
    [InlineData("Platinum", 5000)]
    [InlineData("Unknown", 0)]
    [InlineData(null, 0)]
    public void For_MatchesServerThresholds(string? tier, int expected) =>
        Assert.Equal(expected, LoyaltyTierThresholds.For(tier));

    [Fact]
    public void Progress_SilverWith700Points_IsOneFifthOfTheWayToGold()
    {
        // Arrange
        var summary = Summary(points: 700, tier: "Silver", discount: 2, next: "Gold", missing: 800);

        // Act and Assert
        Assert.Equal(0.2, LoyaltyTierThresholds.Progress(summary), 3);
    }

    [Fact]
    public void Progress_StandardWith250Points_IsHalfwayToSilver()
    {
        // Arrange
        var summary = Summary(points: 250, tier: "Standard", discount: 0, next: "Silver", missing: 250);

        // Act and Assert
        Assert.Equal(0.5, LoyaltyTierThresholds.Progress(summary), 3);
    }

    [Fact]
    public void Progress_TopTier_IsFull()
    {
        // Arrange
        var summary = Summary(points: 7000, tier: "Platinum", discount: 10, next: null, missing: null);

        // Act and Assert
        Assert.Equal(1.0, LoyaltyTierThresholds.Progress(summary));
    }

    [Fact]
    public void Progress_PointsBelowTheTierFloor_IsClampedToZero()
    {
        // Arrange
        var summary = Summary(points: 100, tier: "Silver", discount: 2, next: "Gold", missing: 1400);

        // Act and Assert
        Assert.Equal(0.0, LoyaltyTierThresholds.Progress(summary));
    }

    [Fact]
    public void Progress_UnknownNextTier_FallsBackToServerMissingPoints()
    {
        // Arrange
        var summary = Summary(points: 90, tier: "Standard", discount: 0, next: "Mystery", missing: 10);

        // Act
        var progress = LoyaltyTierThresholds.Progress(summary);

        // Assert
        Assert.Equal(0.9, progress, 3);
    }

    private static LoyaltySummary Summary(int points, string tier, int discount, string? next, int? missing) =>
        new(Guid.NewGuid(), points, tier, discount, next, missing);
}
