using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface ISupplierRepository
{
    Task<(IReadOnlyList<SupplierView> Items, int TotalCount)> SearchAsync(
        SupplierSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SupplierView?> GetViewByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Supplier?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Supplier?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId = null, CancellationToken cancellationToken = default);

    void Add(Supplier supplier);
}
