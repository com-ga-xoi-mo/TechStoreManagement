using TechStore.Client.Models.Catalog;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductCardViewModelTests
{
    [Fact]
    public void SelectingVariant_UpdatesSkuPriceAvailabilityAndStatusForThatCard()
    {
        var first = new ProductVariantSummary("v1", "256GB", "SKU-256", 10_000_000m, 5);
        var second = new ProductVariantSummary("v2", "512GB", "SKU-512", 12_000_000m, 0);
        var product = Product("p1", ProductStatus.InStock, first, second);
        var card = new ProductCardViewModel(product);

        card.SelectedVariant = card.Variants[1];

        Assert.Equal("SKU-512", card.CurrentSku);
        Assert.Equal(12_000_000m, card.CurrentPrice);
        Assert.Equal(0, card.CurrentAvailableQuantity);
        Assert.Equal(ProductStatus.OutOfStock, card.CurrentStatus);
    }

    [Fact]
    public void SelectingVariantOnOneCard_DoesNotChangeAnotherCard()
    {
        var first = new ProductVariantSummary("v1", "256GB", "SKU-256", 10_000_000m, 5);
        var second = new ProductVariantSummary("v2", "512GB", "SKU-512", 12_000_000m, 2);
        var product = Product("p1", ProductStatus.LowStock, first, second);
        var firstCard = new ProductCardViewModel(product);
        var secondCard = new ProductCardViewModel(product);

        firstCard.SelectedVariant = firstCard.Variants[1];

        Assert.Equal("SKU-512", firstCard.CurrentSku);
        Assert.Equal("SKU-256", secondCard.CurrentSku);
        Assert.Equal(10_000_000m, secondCard.CurrentPrice);
        Assert.Equal(5, secondCard.CurrentAvailableQuantity);
        Assert.Equal(ProductStatus.LowStock, secondCard.CurrentStatus);
    }

    [Fact]
    public void DiscontinuedProduct_KeepsDiscontinuedStatusAcrossVariantSelection()
    {
        var first = new ProductVariantSummary("v1", "Base", "SKU-1", 100m, 5);
        var second = new ProductVariantSummary("v2", "Pro", "SKU-2", 200m, 0);
        var card = new ProductCardViewModel(Product("p1", ProductStatus.Discontinued, first, second));

        card.SelectedVariant = card.Variants[1];

        Assert.Equal(0, card.CurrentAvailableQuantity);
        Assert.Equal(ProductStatus.Discontinued, card.CurrentStatus);
    }

    private static ProductSummary Product(
        string id,
        ProductStatus status,
        params ProductVariantSummary[] variants) => new(
        id,
        "Test Device",
        "Test Brand",
        "phones",
        "Điện thoại",
        null,
        status,
        variants,
        DateTimeOffset.UnixEpoch);
}
