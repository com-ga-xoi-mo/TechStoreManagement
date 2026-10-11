using System.Collections.Generic;

namespace TechStore.Client.Models.Catalog;

public sealed record ProductSpecification(string Name, string Value);

/// <summary>
/// Catalog detail returned by the selected repository. Financial values remain optional and
/// are presentation-gated by the current session; this model does not represent authorization.
/// </summary>
public sealed record ProductDetail(
    string Id,
    string Name,
    string Brand,
    string? ModelId,
    int? WarrantyMonths,
    string? ImageUrl,
    ProductStatus Status,
    IReadOnlyList<ProductSpecification> Specifications,
    IReadOnlyList<ProductDetailVariant> Variants,
    bool IsPreviewData);

/// <summary>
/// One actual product configuration. Storage and color form a tuple that exists in the catalog;
/// consumers must not create combinations by cross-joining labels.
/// </summary>
public sealed record ProductDetailVariant(
    string Id,
    string Name,
    string Sku,
    string? StorageLabel,
    string? ColorLabel,
    string? SwatchColor,
    decimal SellingPrice,
    int AvailableQuantity,
    ProductStatus Status,
    IReadOnlyList<ProductSpecification> Specifications,
    decimal? AverageCost,
    decimal? EstimatedMargin);
