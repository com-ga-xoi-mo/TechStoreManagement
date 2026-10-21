using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>Spec definitions looked up by key or by name ignoring case and accents (UI design D16).</summary>
public sealed class SpecCatalog
{
    public const int MaxSuggestions = 6;
    public static SpecCatalog Empty { get; } = new([]);

    private readonly Dictionary<string, SpecDefinition> _byKey;
    private readonly Dictionary<string, SpecDefinition> _byName;

    public SpecCatalog(IEnumerable<SpecDefinition> definitions)
    {
        Definitions = definitions.ToList();
        _byKey = Definitions.GroupBy(d => d.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _byName = Definitions.GroupBy(d => SpecKeyGenerator.Slug(d.Label), StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    public IReadOnlyList<SpecDefinition> Definitions { get; }

    public SpecDefinition? FindByKey(string? key) =>
        key is not null && _byKey.TryGetValue(key, out var definition) ? definition : null;

    /// <summary>A definition whose name equals <paramref name="name"/> ignoring case and accents.</summary>
    public SpecDefinition? FindByName(string? name)
    {
        var slug = SpecKeyGenerator.Slug(name);
        return slug.Length > 0 && _byName.TryGetValue(slug, out var definition) ? definition : null;
    }

    /// <summary>Definitions whose name or key contains the typed text ignoring case and accents, best first.</summary>
    public IReadOnlyList<SpecDefinition> Suggest(string? text)
    {
        var slug = SpecKeyGenerator.Slug(text);
        if (slug.Length == 0) return [];
        return Definitions
            .Select(d => (Definition: d, Name: SpecKeyGenerator.Slug(d.Label)))
            .Where(x => x.Name.Contains(slug, StringComparison.Ordinal) || x.Definition.Key.Contains(slug, StringComparison.Ordinal))
            .OrderBy(x => x.Name.StartsWith(slug, StringComparison.Ordinal) || x.Definition.Key.StartsWith(slug, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x.Definition.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxSuggestions)
            .Select(x => x.Definition)
            .ToList();
    }
}
