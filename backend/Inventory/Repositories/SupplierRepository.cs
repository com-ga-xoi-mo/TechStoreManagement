using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public class SupplierRepository(AppDbContext dbContext) : ISupplierRepository
{
    public async Task<(IReadOnlyList<SupplierView> Items, int TotalCount)> SearchAsync(
        SupplierSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Suppliers
            .AsNoTracking()
            .Where(s => s.Name != SystemSuppliers.OpeningBalanceName);

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == criteria.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, pattern, "!") ||
                (s.ContactName != null && EF.Functions.ILike(s.ContactName, pattern, "!")) ||
                EF.Functions.ILike(s.Phone, pattern, "!") ||
                (s.Email != null && EF.Functions.ILike(s.Email, pattern, "!")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(s => new SupplierView(s, s.PurchaseOrders.Count))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<SupplierView?> GetViewByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierView(s, s.PurchaseOrders.Count))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Supplier?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Supplier?> FindByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Suppliers.AsNoTracking();
        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        var lowerName = normalizedName.Trim().ToLower();
        return query.AnyAsync(s => s.Name.Trim().ToLower() == lowerName, cancellationToken);
    }

    public void Add(Supplier supplier) => dbContext.Suppliers.Add(supplier);

    private static string EscapeLikePattern(string value) => value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
}
