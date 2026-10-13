using System;
using System.Collections.Generic;
using System.Linq;

namespace TechStore.Client.Models.Catalog;

public enum ProductStatus
{
    InStock,
    LowStock,
    OutOfStock,
    Discontinued
}

public enum ProductPriceBand
{
    All,
    Under10Million,
    From10To25Million,
    Above25Million
}

public enum ProductSortMode
{
    Newest,
    PriceAscending,
    PriceDescending,
    StockDescending
}

public sealed record ProductVariantSummary(
    string Id,
    string Name,
    string Sku,
    decimal SellingPrice,
    int AvailableQuantity,
    string? SwatchLabel = null,
    string? SwatchColor = null,
    string? Barcode = null);

public sealed record ProductSummary(
    string Id,
    string Name,
    string Brand,
    string CategoryId,
    string CategoryName,
    string? ImageUrl,
    ProductStatus Status,
    IReadOnlyList<ProductVariantSummary> Variants,
    DateTimeOffset CreatedAt,
    string? ProductSku = null,
    string? Barcode = null)
{
    public string? Sku => ProductSku ?? Variants.FirstOrDefault()?.Sku;

    public decimal? StartingPrice => Variants.Count == 0
        ? null
        : Variants.Min(variant => variant.SellingPrice);

    public int AvailableQuantity => Variants.Sum(variant => variant.AvailableQuantity);
}

public sealed record CategoryOption(string Id, string Name, bool IsPreviewData = true);

public sealed record ProductSpecificationEntry(string Key, string Value);

public sealed record ProductVariantDraft(
    string Name,
    string Sku,
    string? Barcode,
    decimal Price,
    IReadOnlyList<ProductSpecificationEntry> Specifications);

/// <summary>
/// Client-side catalog fields only. Inventory receipt and draft persistence are separate workflows.
/// </summary>
public sealed record ProductCreateDraft(
    string Name,
    string Brand,
    string CategoryId,
    string ProductSku,
    string? Barcode,
    bool IsSerialTracked,
    decimal BasePrice,
    decimal CostPrice,
    string? ImageUrl,
    IReadOnlyList<ProductSpecificationEntry> Specifications,
    IReadOnlyList<ProductVariantDraft> Variants);

public enum ProductCreateResultStatus
{
    Created,
    Conflict,
    Failed
}

public sealed record ProductCreateResult(
    ProductCreateResultStatus Status,
    ProductSummary? Product = null,
    string? FieldName = null,
    string? Message = null)
{
    public bool IsSuccess => Status == ProductCreateResultStatus.Created && Product is not null;
}

public sealed record ProductListCriteria(
    string? SearchQuery = null,
    string? CategoryId = null,
    string? Brand = null,
    ProductPriceBand PriceBand = ProductPriceBand.All,
    ProductStatus? StockStatus = null,
    ProductSortMode SortMode = ProductSortMode.Newest,
    int PageIndex = 0,
    int PageSize = 8);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalItems,
    int PageIndex,
    int PageSize)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public int FirstItemNumber => TotalItems == 0 ? 0 : PageIndex * PageSize + 1;

    public int LastItemNumber => TotalItems == 0
        ? 0
        : Math.Min((PageIndex + 1) * PageSize, TotalItems);
}

public static class ProductPriceBandExtensions
{
    public static bool Includes(this ProductPriceBand band, decimal? price) =>
        band switch
        {
            ProductPriceBand.All => true,
            ProductPriceBand.Under10Million => price is < 10_000_000m,
            ProductPriceBand.From10To25Million => price is >= 10_000_000m and <= 25_000_000m,
            ProductPriceBand.Above25Million => price is > 25_000_000m,
            _ => false
        };
}
