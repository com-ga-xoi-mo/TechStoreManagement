using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;

namespace TechStore.UnitTests;

public class FakeSpecDefinitionRepository : ISpecDefinitionRepository
{
    public List<SpecDefinition> Definitions { get; } = [];
    public List<SpecDefinition> Added { get; } = [];

    public FakeSpecDefinitionRepository With(string key, string label, string? unit, string kind)
    {
        Definitions.Add(new SpecDefinition { Key = key, Label = label, Unit = unit, ValueKind = kind });
        return this;
    }

    public Task<IReadOnlyList<SpecDefinition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SpecDefinition>>(Definitions.OrderBy(d => d.Label).ToList());

    public Task<IReadOnlyList<SpecDefinition>> GetByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var set = keys.ToHashSet(StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<SpecDefinition>>(Definitions.Where(d => set.Contains(d.Key)).ToList());
    }

    public Task<IReadOnlyList<SpecDefinition>> FindByLabelsAsync(IEnumerable<string> labels, CancellationToken cancellationToken = default)
    {
        var set = labels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<SpecDefinition>>(Definitions.Where(d => set.Contains(d.Label)).ToList());
    }

    public void Add(SpecDefinition definition) => Added.Add(definition);
}
