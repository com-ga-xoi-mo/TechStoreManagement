using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public sealed class UnavailableCategoryRepository : ICategoryRepository
{
    public Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Category API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Category-tree API integration is not configured. Enable explicit preview mode to use sample data.");
    }
}
