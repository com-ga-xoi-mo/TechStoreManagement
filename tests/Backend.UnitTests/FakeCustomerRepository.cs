using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;

namespace TechStore.UnitTests;

public class FakeCustomerRepository : ICustomerRepository
{
    public Dictionary<Guid, Customer> Customers { get; } = new();

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Customers.TryGetValue(id, out var customer);
        return Task.FromResult(customer);
    }
}
