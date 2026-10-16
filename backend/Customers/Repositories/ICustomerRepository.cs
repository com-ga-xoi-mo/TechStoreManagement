using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

// Minimal repository created by Orders (Dev 4) so order creation can check a customer
// without depending on Customers services. Dev 5 owns this module and extends it.
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CustomerLoyaltyPoint> Items, int TotalCount)> GetLoyaltyPointsAsync(CustomerLoyaltyPointSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<bool> PhoneExistsAsync(string phone, Guid? excludingCustomerId = null, CancellationToken cancellationToken = default);
    void Add(Customer customer);
}
