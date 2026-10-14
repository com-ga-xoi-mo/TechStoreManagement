using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryOption>> GetTreeAsync(CancellationToken cancellationToken = default);
}
