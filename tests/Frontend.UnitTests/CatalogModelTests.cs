using TechStore.Client.Models.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class CatalogModelTests
{
    [Fact]
    public void PriceBands_UseInclusiveBoundariesAtTenAndTwentyFiveMillion()
    {
        Assert.True(ProductPriceBand.Under10Million.Includes(9_999_999m));
        Assert.False(ProductPriceBand.Under10Million.Includes(10_000_000m));

        Assert.True(ProductPriceBand.From10To25Million.Includes(10_000_000m));
        Assert.True(ProductPriceBand.From10To25Million.Includes(25_000_000m));
        Assert.False(ProductPriceBand.From10To25Million.Includes(25_000_001m));

        Assert.False(ProductPriceBand.Above25Million.Includes(25_000_000m));
        Assert.True(ProductPriceBand.Above25Million.Includes(25_000_001m));
    }

    [Fact]
    public void AllPriceBand_IncludesProductsWithoutPricedVariants()
    {
        Assert.True(ProductPriceBand.All.Includes(null));
        Assert.False(ProductPriceBand.Under10Million.Includes(null));
    }

    [Fact]
    public void ProductSummary_ExposesVariantSkuStartingPriceAndAggregateAvailability()
    {
        var product = CreateProduct(
            imageUrl: null,
            variants:
            [
                new ProductVariantSummary("blue", "Titan Xanh", "IP15-BLUE", 34_990_000m, 3, "Titan Xanh", "#243447"),
                new ProductVariantSummary("white", "Titan Trắng", "IP15-WHITE", 35_990_000m, 2, "Titan Trắng", "#E5E7EB")
            ]);

        Assert.Null(product.ImageUrl);
        Assert.Equal("IP15-BLUE", product.Sku);
        Assert.Equal(34_990_000m, product.StartingPrice);
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Equal("#243447", product.Variants[0].SwatchColor);
    }

    [Fact]
    public void ProductSummaryWithoutVariantsHasNoStartingPriceOrSku()
    {
        var product = CreateProduct(imageUrl: null, variants: []);

        Assert.Null(product.Sku);
        Assert.Null(product.StartingPrice);
        Assert.Equal(0, product.AvailableQuantity);
    }

    [Fact]
    public void PagedResult_ReportsPageRangeAndCountForZeroBasedPageIndex()
    {
        var page = new PagedResult<string>(["p9", "p10"], TotalItems: 10, PageIndex: 1, PageSize: 8);

        Assert.Equal(2, page.TotalPages);
        Assert.Equal(9, page.FirstItemNumber);
        Assert.Equal(10, page.LastItemNumber);
    }

    private static ProductSummary CreateProduct(
        string? imageUrl,
        IReadOnlyList<ProductVariantSummary> variants) =>
        new(
            Id: "product-1",
            Name: "iPhone 15 Pro Max",
            Brand: "Apple",
            CategoryId: "phones",
            CategoryName: "Điện thoại",
            ImageUrl: imageUrl,
            Status: ProductStatus.InStock,
            Variants: variants,
            CreatedAt: new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
}
