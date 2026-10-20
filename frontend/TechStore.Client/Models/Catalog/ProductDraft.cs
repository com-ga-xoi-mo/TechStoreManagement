namespace TechStore.Client.Models.Catalog;

public sealed record SpecDraft(string Key, SpecValueKind Kind, string Value);

public sealed record VariantDraft(
    Guid? Id,
    string VariantName,
    string Sku,
    string Price,
    string Barcode,
    bool IsActive,
    IReadOnlyList<SpecDraft> Specs);

public sealed record ProductDraft(
    Guid? CategoryId,
    string Name,
    string Sku,
    string Brand,
    string BasePrice,
    string CostPrice,
    string Barcode,
    string Description,
    string ImageUrl,
    bool IsSerialTracked,
    IReadOnlyList<SpecDraft> Specs,
    IReadOnlyList<VariantDraft> Variants);
