using System.Collections.Generic;

namespace TechStore.Client.Models.Catalog;

public sealed record ProductEditQuery(string ProductId, bool IncludeCostFields);

/// <summary>
/// Readable catalog state supplied to the edit form. Stock and serial tracking are informational
/// only; cost values are omitted when the selected source does not grant preview cost-write access.
/// </summary>
public sealed record ProductEditData(
    string Id,
    string Name,
    string Brand,
    string CategoryId,
    string CategoryName,
    string ProductSku,
    string? Barcode,
    string? ModelId,
    string? ImageUrl,
    decimal? BasePrice,
    decimal? CostPrice,
    bool IsSerialTracked,
    int AvailableQuantity,
    IReadOnlyList<ProductSpecificationEntry> Specifications,
    IReadOnlyList<ProductEditVariant> Variants,
    bool IsPreviewData);

/// <summary>
/// One existing catalog variant presented by the product editor.
/// </summary>
public sealed record ProductEditVariant(
    string Id,
    string Name,
    string Sku,
    string? Barcode,
    string? StorageLabel,
    string? ColorLabel,
    string? SwatchColor,
    decimal SellingPrice,
    decimal? CostPrice,
    int AvailableQuantity,
    IReadOnlyList<ProductSpecificationEntry> Specifications);

/// <summary>
/// Client-only mutable intent submitted by the edit form. It intentionally has no inventory,
/// transaction-history, or product-delete field.
/// </summary>
public sealed record ProductEditDraft(
    string ProductId,
    string Name,
    string Brand,
    string CategoryId,
    string ProductSku,
    string? Barcode,
    string? ModelId,
    string? ImageUrl,
    decimal? BasePrice,
    decimal? CostPrice,
    bool IsSerialTracked,
    IReadOnlyList<ProductSpecificationEntry> Specifications,
    IReadOnlyList<ProductEditVariantDraft> Variants);

/// <summary>
/// One variant's submitted values; availability and transaction history are not editable here.
/// </summary>
public sealed record ProductEditVariantDraft(
    string? Id,
    string Name,
    string Sku,
    string? Barcode,
    string? StorageLabel,
    string? ColorLabel,
    string? SwatchColor,
    decimal SellingPrice,
    decimal? CostPrice,
    IReadOnlyList<ProductSpecificationEntry> Specifications);

public enum ProductEditResultStatus
{
    Saved,
    Conflict,
    Failed
}

public enum ProductEditConflictKind
{
    None,
    DuplicateSku,
    DuplicateBarcode,
    VariantHasTransactions,
    ConcurrentUpdate
}

public sealed record ProductEditResult(
    ProductEditResultStatus Status,
    ProductEditData? Product = null,
    string? FieldName = null,
    ProductEditConflictKind ConflictKind = ProductEditConflictKind.None,
    string? Message = null)
{
    public bool IsSuccess => Status == ProductEditResultStatus.Saved && Product is not null;
}
