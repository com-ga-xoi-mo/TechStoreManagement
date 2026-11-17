using TechStore.Api.Inventory;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;

namespace TechStore.UnitTests;

public class FakeSupplierRepository : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public Task<(IReadOnlyList<SupplierView> Items, int TotalCount)> SearchAsync(
        SupplierSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = Suppliers
            .Where(s => s.Name != SystemSuppliers.OpeningBalanceName)
            .AsQueryable();

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == criteria.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            query = query.Where(s =>
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.ContactName != null && s.ContactName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                s.Phone.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.Email != null && s.Email.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(s => s.Name)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .AsEnumerable()
            .Select(s => new SupplierView(s, s.PurchaseOrders != null ? s.PurchaseOrders.Count : 0))
            .ToList();

        return Task.FromResult(((IReadOnlyList<SupplierView>)items, totalCount));
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.FirstOrDefault(s => s.Id == id));

    public Task<SupplierView?> GetViewByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = Suppliers.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(s != null ? new SupplierView(s, s.PurchaseOrders != null ? s.PurchaseOrders.Count : 0) : null);
    }

    public Task<Supplier?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.FirstOrDefault(s => s.Id == id));

    public Task<Supplier?> FindByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.FirstOrDefault(s => s.Name == name));

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = normalizedName.Trim().ToLowerInvariant();
        return Task.FromResult(Suppliers.Any(s =>
            (!excludeId.HasValue || s.Id != excludeId.Value) &&
            s.Name.Trim().ToLowerInvariant() == lower));
    }

    public void Add(Supplier supplier) => Suppliers.Add(supplier);
}
