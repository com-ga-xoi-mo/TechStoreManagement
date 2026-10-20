using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

internal static class ProductCommandTestData
{
    public static Category NewActiveCategory(string name = "Laptops") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = name.ToLowerInvariant(),
        IsActive = true,
        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    public static Product NewProduct(
        Guid categoryId,
        string sku = "MBA-M3",
        bool serialTracked = true,
        bool isActive = true,
        string? barcode = null) => new()
    {
        Id = Guid.NewGuid(),
        CategoryId = categoryId,
        Name = "MacBook Air M3",
        Sku = sku,
        Brand = "Apple",
        Barcode = barcode,
        BasePrice = 27990000m,
        CostPrice = 24000000m,
        Specs = "{\"cpu\":\"Apple M3\",\"ram_gb\":8}",
        IsSerialTracked = serialTracked,
        IsActive = isActive,
        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = null
    };

    public static ProductVariant NewVariant(
        Product product,
        string sku,
        decimal price = 27990000m,
        bool isActive = true)
    {
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            VariantName = sku,
            Sku = sku,
            Price = price,
            Specs = "{}",
            IsActive = isActive,
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = null
        };
        product.Variants.Add(variant);
        return variant;
    }

    public static UpdateProductRequest NewRequestFrom(Product product) => new()
    {
        CategoryId = product.CategoryId,
        Name = product.Name,
        Brand = product.Brand,
        Barcode = product.Barcode,
        BasePrice = product.BasePrice,
        CostPrice = product.CostPrice,
        Description = product.Description,
        ImageUrl = product.ImageUrl,
        Specs = ProductSpecHelper.ParseJsonSpecs(product.Specs),
        IsSerialTracked = product.IsSerialTracked,
        Variants = null
    };
}
