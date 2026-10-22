using System.Text.Json;

namespace TechStore.Client.Models.Catalog;

public sealed record ProductDetail(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Sku,
    string? Barcode,
    string Brand,
    decimal BasePrice,
    decimal CostPrice,
    string? Description,
    string? ImageUrl,
    IReadOnlyDictionary<string, JsonElement> Specs,
    bool IsSerialTracked,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ProductVariant> Variants,
    IReadOnlyList<ProductImage> Images);

public sealed record ProductVariant(
    Guid Id,
    Guid ProductId,
    string VariantName,
    string Sku,
    string? Barcode,
    decimal Price,
    IReadOnlyDictionary<string, JsonElement> Specs,
    IReadOnlyDictionary<string, JsonElement> EffectiveSpecs,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
