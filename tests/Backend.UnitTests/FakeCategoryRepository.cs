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

    public Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Category> active = Categories.Values
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToList();
        return Task.FromResult(active);
    }

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var exists = Categories.Values.Any(c => c.Slug.ToLowerInvariant() == normalized);
        return Task.FromResult(exists);
    }

    public void Add(Category category)
    {
        Categories[category.Id] = category;
    }
}
