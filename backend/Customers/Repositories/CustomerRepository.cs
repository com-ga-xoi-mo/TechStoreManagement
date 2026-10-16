using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Repositories;

public class CustomerRepository(AppDbContext dbContext) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<Customer?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var pattern = $"%{EscapeLikePattern(criteria.Query)}%";
            query = query.Where(c => EF.Functions.ILike(c.FullName, pattern) || EF.Functions.ILike(c.Phone, pattern));
        }
        if (criteria.Tier.HasValue) query = query.Where(c => c.Tier == criteria.Tier.Value);
        if (criteria.IsActive.HasValue) query = query.Where(c => c.IsActive == criteria.IsActive.Value);

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToListAsync(cancellationToken);
        return (items, count);
    }

    public async Task<(IReadOnlyList<CustomerLoyaltyPoint> Items, int TotalCount)> GetLoyaltyPointsAsync(CustomerLoyaltyPointSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = dbContext.CustomerLoyaltyPoints.AsNoTracking().Where(p => p.CustomerId == criteria.CustomerId);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToListAsync(cancellationToken);
        return (items, count);
    }

    public Task<bool> PhoneExistsAsync(string phone, Guid? excludingCustomerId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking().Where(c => c.Phone == phone);
        if (excludingCustomerId.HasValue) query = query.Where(c => c.Id != excludingCustomerId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public void Add(Customer customer) => dbContext.Customers.Add(customer);

    private static string EscapeLikePattern(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
