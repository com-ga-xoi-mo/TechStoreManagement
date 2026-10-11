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
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        return await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug.ToLower() == normalizedSlug, cancellationToken);
    }

    public void Add(Category category)
    {
        dbContext.Categories.Add(category);
    }
}
