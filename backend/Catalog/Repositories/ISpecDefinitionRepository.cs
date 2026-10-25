using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Repositories;

public interface ISpecDefinitionRepository
{
    Task<IReadOnlyList<SpecDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpecDefinition>> GetByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>Definitions whose label equals one of <paramref name="labels"/> ignoring letter case.</summary>
    Task<IReadOnlyList<SpecDefinition>> FindByLabelsAsync(IEnumerable<string> labels, CancellationToken cancellationToken = default);

    void Add(SpecDefinition definition);
}
