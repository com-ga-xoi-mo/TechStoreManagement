using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default);
}
