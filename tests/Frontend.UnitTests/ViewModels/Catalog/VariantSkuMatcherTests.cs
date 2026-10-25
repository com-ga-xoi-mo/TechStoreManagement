using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class VariantSkuMatcherTests
{
    [Fact]
    public void Match_DifferentOrderAndCase_UsesSku()
    {
        // Arrange
        var black = new VariantRowItem(null) { Sku = " ip16-bk " }; var white = new VariantRowItem(null) { Sku = "IP16-WH" };
        var savedBlack = CatalogSamples.Variant(Guid.NewGuid(), "IP16-BK"); var savedWhite = CatalogSamples.Variant(Guid.NewGuid(), "IP16-WH");
        // Act
        var result = VariantSkuMatcher.Match([black, white], [savedWhite, savedBlack]);
        // Assert
        Assert.Equal(savedBlack.Id, result.Matches[black]); Assert.Equal(savedWhite.Id, result.Matches[white]); Assert.Empty(result.MissingSkus);
    }
    [Fact]
    public void Match_SavedRowsAreSkipped()
    {
        // Arrange
        var row = new VariantRowItem(Guid.NewGuid()) { Sku = "saved" };
        // Act
        var result = VariantSkuMatcher.Match([row], []);
        // Assert
        Assert.Empty(result.Matches); Assert.Empty(result.MissingSkus);
    }
    [Fact]
    public void Match_MissingSku_IsReported()
    {
        // Arrange
        var row = new VariantRowItem(null) { Sku = "missing" };
        // Act
        var result = VariantSkuMatcher.Match([row], []);
        // Assert
        Assert.Equal("missing", Assert.Single(result.MissingSkus));
    }
}
