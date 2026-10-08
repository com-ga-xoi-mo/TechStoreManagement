using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;

namespace TechStore.UnitTests;

public class FakeCategoryRepository : ICategoryRepository
{
    public Dictionary<Guid, Category> Categories { get; } = new();

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Categories.TryGetValue(id, out var category);
        return Task.FromResult(category);
    }
}
