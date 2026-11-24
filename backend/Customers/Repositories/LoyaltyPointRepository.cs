using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

public interface ILoyaltyPointRepository
{
    Task<Customer> LockCustomerAsync(Guid customerId, CancellationToken ct);
    Task<List<CustomerLoyaltyPoint>> OrderEntriesAsync(Guid orderId, Guid customerId, CancellationToken ct);
    void Add(CustomerLoyaltyPoint entry);
}

public sealed class LoyaltyPointRepository(AppDbContext db) : ILoyaltyPointRepository
{
    public Task<Customer> LockCustomerAsync(Guid customerId, CancellationToken ct) =>
        db.Customers.FromSqlInterpolated($"SELECT * FROM customers WHERE id={customerId} FOR UPDATE").SingleAsync(ct);

    public async Task<List<CustomerLoyaltyPoint>> OrderEntriesAsync(Guid orderId, Guid customerId, CancellationToken ct)
    {
        var entries = await db.CustomerLoyaltyPoints.AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.OrderId == orderId && (p.IsSaleAccrual || p.RefundCaseId != null))
            .ToListAsync(ct);
        entries.AddRange(db.ChangeTracker.Entries<CustomerLoyaltyPoint>()
            .Where(e => e.State == EntityState.Added && e.Entity.CustomerId == customerId && e.Entity.OrderId == orderId)
            .Select(e => e.Entity));
        return entries;
    }

    public void Add(CustomerLoyaltyPoint entry) => db.CustomerLoyaltyPoints.Add(entry);
}
