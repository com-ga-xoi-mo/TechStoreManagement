using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

// Minimal repository created by Orders (Dev 4) so order creation can check a customer
// without depending on Customers services. Dev 5 owns this module and extends it.
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
