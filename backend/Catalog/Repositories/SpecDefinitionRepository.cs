using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;

namespace TechStore.Api.Catalog.Repositories;

public class SpecDefinitionRepository(AppDbContext dbContext) : ISpecDefinitionRepository
{
    public async Task<IReadOnlyList<SpecDefinition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SpecDefinitions
            .AsNoTracking()
            .OrderBy(d => d.Label)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SpecDefinition>> GetByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keyList = keys.Distinct(StringComparer.Ordinal).ToList();
        if (keyList.Count == 0) return [];
        return await dbContext.SpecDefinitions
            .AsNoTracking()
            .Where(d => keyList.Contains(d.Key))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpecDefinition>> FindByLabelsAsync(IEnumerable<string> labels, CancellationToken cancellationToken = default)
    {
        var lowered = labels.Select(l => l.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();
        if (lowered.Count == 0) return [];
        return await dbContext.SpecDefinitions
            .AsNoTracking()
            .Where(d => lowered.Contains(d.Label.ToLower()))
            .ToListAsync(cancellationToken);
    }

    public void Add(SpecDefinition definition) => dbContext.SpecDefinitions.Add(definition);
}
