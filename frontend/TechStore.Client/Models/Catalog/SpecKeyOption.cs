namespace TechStore.Client.Models.Catalog;

/// <summary>A stored spec value; <see cref="FilterValue"/> is sent unchanged as <c>key:FilterValue</c>.</summary>
public sealed record SpecValueOption(string Label, string FilterValue, int ProductCount, SpecValueKind Kind);

/// <summary>A spec key that occurs in stored products, with its definition name (if any) and every filterable value.</summary>
public sealed record SpecKeyOption(string Key, int ProductCount, IReadOnlyList<SpecValueOption> Values, string? Label = null, string? Unit = null);
