namespace TechStore.Client.Models.Catalog;

/// <summary>Shared human name, unit and value type of a spec key (API design D13).</summary>
public sealed record SpecDefinition(string Key, string Label, string? Unit, SpecValueKind Kind);
