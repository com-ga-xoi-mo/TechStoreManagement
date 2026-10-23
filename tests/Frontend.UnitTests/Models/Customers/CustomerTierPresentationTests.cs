using TechStore.Client.Models.Customers;

namespace TechStore.Client.UnitTests.Models.Customers;

public class CustomerTierPresentationTests
{
    [Theory]
    [InlineData("Standard", "TechStoreInfoFillBrush", "TechStoreInfoTextBrush")]
    [InlineData("Silver", "TechStoreNeutralFillBrush", "TechStoreNeutralTextBrush")]
    [InlineData("Gold", "TechStoreWarningFillBrush", "TechStoreWarningTextBrush")]
    [InlineData("Platinum", "TechStoreAccentFillBrush", "TechStoreAccentTextBrush")]
    public void FromTier_ReturnsSharedThemePalette(string tier, string fillKey, string textKey)
    {
        var presentation = CustomerTierPresentation.FromTier(tier);

        Assert.Equal(fillKey, presentation.FillResourceKey);
        Assert.Equal(textKey, presentation.TextResourceKey);
    }

    [Fact]
    public void FromTier_UnknownValueFallsBackToStandard()
    {
        var presentation = CustomerTierPresentation.FromTier("Unknown");

        Assert.Equal("Standard", presentation.Tier);
        Assert.Equal("Tiêu chuẩn", presentation.Label);
    }
}
