using TechStore.Api.Customers.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Customers.Repositories;

// Minimal repository created by Orders (Dev 4) so order creation can check a customer
// without depending on Customers services. Dev 5 owns this module and extends it.
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerOrderMetrics?> GetOrderMetricsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CustomerSearchResult> Items, int TotalCount)> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CustomerLoyaltyPoint> Items, int TotalCount)> GetLoyaltyPointsAsync(CustomerLoyaltyPointSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CustomerPurchasedItemDto> Items, int TotalCount)> GetPurchasedItemsAsync(CustomerPurchasedItemSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<bool> PhoneExistsAsync(string phone, Guid? excludingCustomerId = null, CancellationToken cancellationToken = default);
    void Add(Customer customer);
}

public sealed record CustomerSearchResult(
    Customer Customer,
    int OrderCount,
    decimal TotalSpent,
    DateTime? LastPurchaseAt);

public sealed record CustomerOrderMetrics(
    int OrderCount,
    int CompletedOrderCount,
    int CancelledOrderCount,
    decimal TotalSpent,
    DateTime? LastPurchaseAt);
