using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryTreeRow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugExceptIdAsync(string slug, Guid excludedId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasSubCategoriesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasProductsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AcquireHierarchyUpdateLockAsync(CancellationToken cancellationToken = default);
    void Add(Category category);
    void Delete(Category category);
}
