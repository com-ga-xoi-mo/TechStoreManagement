using TechStore.Client.Models.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductDetailModelTests
{
    [Fact]
    public void ProductDetail_PreservesTypedMetadataSpecificationsAndPreviewProvenance()
    {
        var detail = CreateDetail();

        Assert.Equal("iphone-15-pro-max", detail.Id);
        Assert.Equal("A3106", detail.ModelId);
        Assert.Equal(12, detail.WarrantyMonths);
        Assert.Equal("Apple A17 Pro", Assert.Single(detail.Specifications).Value);
        Assert.True(detail.IsPreviewData);
        Assert.Equal(34_990_000m, Assert.Single(detail.Variants).SellingPrice);
    }

    [Fact]
    public void ProductDetail_AllowsMissingOptionalImageWarrantyAndFinancialValues()
    {
        var detail = CreateDetail() with
        {
            ImageUrl = null,
            WarrantyMonths = null,
            Variants =
            [
                CreateVariant() with
                {
                    AverageCost = null,
                    EstimatedMargin = null
                }
            ]
        };

        Assert.Null(detail.ImageUrl);
        Assert.Null(detail.WarrantyMonths);
        Assert.Null(Assert.Single(detail.Variants).AverageCost);
        Assert.Null(Assert.Single(detail.Variants).EstimatedMargin);
    }

    [Fact]
    public void ProductDetailVariant_KeepsMoneyAsDecimalAndSpecificationsTyped()
    {
        var variant = CreateVariant() with
        {
            SellingPrice = 34_990_000.25m,
            AverageCost = 30_500_000.15m,
            EstimatedMargin = 4_490_000.10m,
            Specifications = [new ProductSpecification("Lưu trữ", "256GB")]
        };

        Assert.Equal(34_990_000.25m, variant.SellingPrice);
        Assert.Equal(30_500_000.15m, variant.AverageCost);
        Assert.Equal(4_490_000.10m, variant.EstimatedMargin);
        Assert.Equal("256GB", Assert.Single(variant.Specifications).Value);
    }

    private static ProductDetail CreateDetail() => new(
        Id: "iphone-15-pro-max",
        Name: "iPhone 15 Pro Max",
        Brand: "Apple",
        ModelId: "A3106",
        WarrantyMonths: 12,
        ImageUrl: "ms-appx:///Assets/Catalog/phones.png",
        Status: ProductStatus.InStock,
        Specifications: [new ProductSpecification("CPU", "Apple A17 Pro")],
        Variants: [CreateVariant()],
        IsPreviewData: true);

    private static ProductDetailVariant CreateVariant() => new(
        Id: "iphone-15-pro-max-natural",
        Name: "Titan Tự nhiên 256GB",
        Sku: "IP15PM-256-NAT",
        StorageLabel: "256GB",
        ColorLabel: "Titan Tự nhiên",
        SwatchColor: "#A49E93",
        SellingPrice: 34_990_000m,
        AvailableQuantity: 12,
        Status: ProductStatus.InStock,
        Specifications: [],
        AverageCost: 30_500_000m,
        EstimatedMargin: 4_490_000m);
}
