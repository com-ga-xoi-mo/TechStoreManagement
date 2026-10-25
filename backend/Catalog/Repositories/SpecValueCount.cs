namespace TechStore.Api.Catalog.Repositories;

/// <summary>
/// How many distinct products have a spec key (<see cref="ValueJson"/> null) or one of its scalar values (raw JSON text).
/// </summary>
public sealed class SpecValueCount
{
    public string Key { get; set; } = string.Empty;
    public string? ValueJson { get; set; }
    public int ProductCount { get; set; }
}
