using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

public interface ILoyaltyRepository
{
    // Tracked customer locked with SELECT ... FOR UPDATE so two point changes of one customer never overwrite each other.
    // Requires an open transaction opened by the calling flow; outside one the lock is released immediately.
    Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default);

    // Ledger row stored under an idempotency key such as earn:{orderId}, or null.
    Task<CustomerLoyaltyPoint?> FindByReferenceKeyAsync(string referenceKey, CancellationToken cancellationToken = default);

    // Net points the ledger holds for one order (earned minus reversed).
    Task<int> SumPointsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    void Add(CustomerLoyaltyPoint entry);
}
