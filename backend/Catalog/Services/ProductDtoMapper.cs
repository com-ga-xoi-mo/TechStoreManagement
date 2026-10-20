using TechStore.Api.Catalog.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Catalog.Services;

internal static class ProductDtoMapper
{
    public static ProductDto ToDto(Product product, string categoryName)
    {
        var productSpecs = ProductSpecHelper.ParseJsonSpecs(product.Specs);

        var variantDtos = product.Variants
            .OrderBy(v => v.Price)
            .ThenBy(v => v.Sku)
            .Select(v =>
            {
                var vSpecs = ProductSpecHelper.ParseJsonSpecs(v.Specs);
                var effective = ProductSpecHelper.MergeEffectiveSpecs(productSpecs, vSpecs);

                return new ProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    VariantName = v.VariantName,
                    Sku = v.Sku,
                    Barcode = v.Barcode,
                    Price = v.Price,
                    Specs = vSpecs,
                    EffectiveSpecs = effective,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                };
            })
            .ToList();

        return new ProductDto
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            CategoryName = categoryName,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Brand = product.Brand,
            BasePrice = product.BasePrice,
            CostPrice = product.CostPrice,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            Specs = productSpecs,
            IsSerialTracked = product.IsSerialTracked,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Variants = variantDtos
        };
    }
}
