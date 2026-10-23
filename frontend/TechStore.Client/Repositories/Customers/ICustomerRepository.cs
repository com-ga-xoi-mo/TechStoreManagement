using TechStore.Client.Models.Customers;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Customers;

public interface ICustomerRepository
{
    Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default);
    Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default);
}
