using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

public class LoyaltyRepository(AppDbContext dbContext) : ILoyaltyRepository
{
    public Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        dbContext.Customers
            .FromSqlInterpolated($"SELECT * FROM customers WHERE id = {customerId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CustomerLoyaltyPoint?> FindByReferenceKeyAsync(string referenceKey, CancellationToken cancellationToken = default) =>
        dbContext.CustomerLoyaltyPoints.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ReferenceKey == referenceKey, cancellationToken);

    public async Task<int> SumPointsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await dbContext.CustomerLoyaltyPoints.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .SumAsync(p => (int?)p.PointsChange, cancellationToken) ?? 0;

    public void Add(CustomerLoyaltyPoint entry) => dbContext.CustomerLoyaltyPoints.Add(entry);
}
