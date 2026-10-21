using System.Text.Json;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using Xunit;

namespace TechStore.Client.UnitTests.Controls;

public sealed class CatalogPresentationTests
{
    [Fact]
    public void FormatMoney_FormatsWithThousandsSeparatorsAndCurrencySymbol()
    {
        // Arrange & Act & Assert
        Assert.Equal("35.990.000 ₫", CatalogPresentation.FormatMoney(35990000m));
        Assert.Equal("0 ₫", CatalogPresentation.FormatMoney(0m));
    }

    [Fact]
    public void PriceRange_EqualValues_ReturnsSinglePrice()
    {
        // Arrange & Act
        var result = CatalogPresentation.PriceRange(20000000m, 20000000m);

        // Assert
        Assert.Equal("20.000.000 ₫", result);
    }

    [Fact]
    public void PriceRange_DifferentValues_ReturnsRangeWithEnDash()
    {
        // Arrange & Act
        var result = CatalogPresentation.PriceRange(20000000m, 25000000m);

        // Assert
        Assert.Equal("20.000.000 – 25.000.000 ₫", result);
    }

    [Fact]
    public void ActiveLabelAndTone_ReturnsExpectedValues()
    {
        // Arrange & Act & Assert
        Assert.Equal("Đang hoạt động", CatalogPresentation.ActiveLabel(true));
        Assert.Equal(StatusTone.Success, CatalogPresentation.ActiveTone(true));

        Assert.Equal("Ngừng hoạt động", CatalogPresentation.ActiveLabel(false));
        Assert.Equal(StatusTone.Neutral, CatalogPresentation.ActiveTone(false));
    }

    [Fact]
    public void VariantCountLabel_ReturnsSingularOrPluralFormat()
    {
        // Arrange & Act & Assert
        Assert.Equal("Không có biến thể", CatalogPresentation.VariantCountLabel(0));
        Assert.Equal("3 biến thể", CatalogPresentation.VariantCountLabel(3));
    }

    [Fact]
    public void OptionalText_NullOrWhitespace_ReturnsEmDash()
    {
        // Arrange & Act & Assert
        Assert.Equal("—", CatalogPresentation.OptionalText(null));
        Assert.Equal("—", CatalogPresentation.OptionalText(""));
        Assert.Equal("—", CatalogPresentation.OptionalText("   "));
        Assert.Equal("ABC", CatalogPresentation.OptionalText("ABC"));
    }

    [Fact]
    public void ResolveImageUri_HandlesNullInvalidAndValidUris()
    {
        // Arrange
        var apiBase = new Uri("http://localhost:5176/");

        // Act & Assert
        Assert.Null(CatalogPresentation.ResolveImageUri(null, apiBase));
        Assert.Null(CatalogPresentation.ResolveImageUri("not a url", apiBase));
        Assert.Null(CatalogPresentation.ResolveImageUri("ftp://x/a.png", apiBase));

        var absolute = CatalogPresentation.ResolveImageUri("https://cdn.test/a.png", apiBase);
        Assert.NotNull(absolute);
        Assert.Equal("https://cdn.test/a.png", absolute.ToString());

        var relative = CatalogPresentation.ResolveImageUri("/images/a.png", apiBase);
        Assert.NotNull(relative);
        Assert.Equal("http://localhost:5176/images/a.png", relative.ToString());
    }

    [Fact]
    public void MarginText_CalculatesProfitLossOrNull()
    {
        // Arrange & Act
        var profit = CatalogPresentation.MarginText(35990000m, 31200000m);
        var loss = CatalogPresentation.MarginText(1000000m, 1200000m);
        var zeroBase = CatalogPresentation.MarginText(0m, 5m);

        // Assert
        Assert.NotNull(profit);
        Assert.Equal("Lãi 4.790.000 ₫ (13,3%)", profit.Text);
        Assert.False(profit.IsLoss);

        Assert.NotNull(loss);
        Assert.Equal("Lỗ 200.000 ₫", loss.Text);
        Assert.True(loss.IsLoss);

        Assert.Null(zeroBase);
    }
}
