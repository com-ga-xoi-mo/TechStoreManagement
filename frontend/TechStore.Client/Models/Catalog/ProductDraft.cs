namespace TechStore.Client.Models.Catalog;

public sealed record SpecDraft(string Key, SpecValueKind Kind, string Value);

/// <summary>Name to save for a spec key that has no definition yet (API design D13).</summary>
public sealed record SpecLabelDraft(string Key, string Label, string? Unit, SpecValueKind Kind);

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
    bool IsSerialTracked,
    IReadOnlyList<SpecDraft> Specs,
    IReadOnlyList<VariantDraft> Variants,
    IReadOnlyList<SpecLabelDraft>? SpecLabels = null);
