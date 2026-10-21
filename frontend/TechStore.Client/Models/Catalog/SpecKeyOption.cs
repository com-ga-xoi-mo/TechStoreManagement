namespace TechStore.Client.Models.Catalog;

/// <summary>A stored spec value; <see cref="FilterValue"/> is sent unchanged as <c>key:FilterValue</c>.</summary>
public sealed record SpecValueOption(string Label, string FilterValue, int ProductCount)
{
    public string DisplayName => $"{Label} ({ProductCount})";
}

/// <summary>A spec key that occurs in stored products, with every value the filter can match.</summary>
public sealed record SpecKeyOption(string Key, int ProductCount, IReadOnlyList<SpecValueOption> Values)
{
    public string DisplayName => $"{Key} ({ProductCount})";
}
