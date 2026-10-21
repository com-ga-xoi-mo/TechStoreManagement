using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>A ComboBox item: <see cref="Value"/> is what is stored or sent, <see cref="DisplayName"/> what is shown.</summary>
public sealed record SpecChoice(string Value, string DisplayName);

/// <summary>A definition offered under a spec name box; picking it fills <see cref="Row"/>.</summary>
public sealed record SpecSuggestion(SpecRowItem Row, SpecDefinition Definition)
{
    public string DisplayName => Definition.Unit is { Length: > 0 } unit
        ? $"{Definition.Label} ({unit}) · {Definition.Key}"
        : $"{Definition.Label} · {Definition.Key}";
}

/// <summary>A formatted spec line of the product detail page.</summary>
public sealed record SpecLine(string Name, string Value);

/// <summary>A variant of the product detail page with its effective specs already formatted.</summary>
public sealed record VariantLine(ProductVariant Variant, string SpecsText);
