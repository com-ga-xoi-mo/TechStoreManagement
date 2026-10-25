using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;

namespace TechStore.Api.Catalog.Repositories;

public class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .Include(c => c.Products)
            .ThenInclude(p => p.Variants)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Category?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        return await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug.ToLower() == normalizedSlug, cancellationToken);
    }

    public Task<bool> ExistsBySlugExceptIdAsync(string slug, Guid excludedId, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        return dbContext.Categories.AsNoTracking()
            .AnyAsync(c => c.Id != excludedId && c.Slug.ToLower() == normalizedSlug, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var links = await dbContext.Categories.AsNoTracking()
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync(cancellationToken);
        var children = links.Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());
        var result = new List<Guid>();
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid> { id };
        pending.Push(id);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!children.TryGetValue(current, out var direct)) continue;
            foreach (var child in direct)
            {
                if (!visited.Add(child)) continue;
                result.Add(child);
                pending.Push(child);
            }
        }
        return result;
    }

    public Task<bool> HasSubCategoriesAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AsNoTracking().AnyAsync(c => c.ParentId == id, cancellationToken);

    public Task<bool> HasProductsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products.AsNoTracking().AnyAsync(p => p.CategoryId == id, cancellationToken);

    public async Task AcquireHierarchyUpdateLockAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A category hierarchy transaction is required before acquiring the lock.");
        if (dbContext.Database.IsRelational())
            await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731204620261019)", cancellationToken);
    }

    public void Add(Category category)
    {
        dbContext.Categories.Add(category);
    }

    public void Delete(Category category) => dbContext.Categories.Remove(category);
}
