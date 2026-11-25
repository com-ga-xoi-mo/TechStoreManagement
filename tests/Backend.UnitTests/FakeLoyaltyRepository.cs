using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;

namespace TechStore.UnitTests;

public class FakeLoyaltyRepository : ILoyaltyRepository
{
    public Dictionary<Guid, Customer> Customers { get; } = new();
    public List<CustomerLoyaltyPoint> Entries { get; } = [];

    public Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Customers.TryGetValue(customerId, out var customer);
        return Task.FromResult(customer);
    }

    public Task<CustomerLoyaltyPoint?> FindByReferenceKeyAsync(string referenceKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entries.FirstOrDefault(e => e.ReferenceKey == referenceKey));

    public Task<int> SumPointsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entries.Where(e => e.OrderId == orderId).Sum(e => e.PointsChange));

    public void Add(CustomerLoyaltyPoint entry) => Entries.Add(entry);
}
