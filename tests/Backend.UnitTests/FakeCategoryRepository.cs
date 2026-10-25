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

    public Task<Category?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Category> active = Categories.Values
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

    public Task<bool> ExistsBySlugExceptIdAsync(string slug, Guid excludedId, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var exists = Categories.Values.Any(c => c.Id != excludedId && c.Slug.ToLowerInvariant() == normalized);
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var descendants = new List<Guid>();
        var pending = new Queue<Guid>(Categories.Values.Where(c => c.ParentId == categoryId).Select(c => c.Id));
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (descendants.Contains(id)) continue;
            descendants.Add(id);
            foreach (var child in Categories.Values.Where(c => c.ParentId == id)) pending.Enqueue(child.Id);
        }
        return Task.FromResult<IReadOnlyList<Guid>>(descendants);
    }

    public Task<bool> HasSubCategoriesAsync(Guid categoryId, CancellationToken cancellationToken = default)
        => Task.FromResult(Categories.Values.Any(c => c.ParentId == categoryId));

    public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
        => Task.FromResult(Categories.TryGetValue(categoryId, out var category) && category.Products.Count > 0);

    public Task AcquireHierarchyUpdateLockAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Delete(Category category) => Categories.Remove(category.Id);

    public void Add(Category category)
    {
        Categories[category.Id] = category;
    }
}
