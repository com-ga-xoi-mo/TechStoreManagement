using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public class FakeCustomerRepository : ICustomerRepository
{
    public Dictionary<Guid, Customer> Customers { get; } = new();
    public List<CustomerLoyaltyPoint> PointEntries { get; } = [];
    public List<Order> Orders { get; } = [];
    public List<(Guid CustomerId, CustomerPurchasedItemDto Item)> PurchasedItems { get; } = [];

    public Task<Customer?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<CustomerOrderMetrics?> GetOrderMetricsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!Customers.ContainsKey(id)) return Task.FromResult<CustomerOrderMetrics?>(null);
        var orders = Orders.Where(o => o.CustomerId == id).ToList();
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        return Task.FromResult<CustomerOrderMetrics?>(new(
            orders.Count, completed.Count, orders.Count(o => o.Status == OrderStatus.Cancelled),
            completed.Sum(o => o.TotalAmount), completed.Max(o => (DateTime?)o.CompletedAt)));
    }

    public Task<(IReadOnlyList<CustomerSearchResult> Items, int TotalCount)> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        IEnumerable<Customer> query = Customers.Values;
        if (criteria.Query != null) query = query.Where(c => c.FullName.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase) || c.Phone.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase));
        if (criteria.Tier.HasValue) query = query.Where(c => c.Tier == criteria.Tier.Value);
        if (criteria.IsActive.HasValue) query = query.Where(c => c.IsActive == criteria.IsActive.Value);
        var all = query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).ToList();
        var items = all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize)
            .Select(c =>
            {
                var orders = Orders.Where(o => o.CustomerId == c.Id).ToList();
                var completedOrders = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
                return new CustomerSearchResult(
                    c,
                    orders.Count,
                    completedOrders.Sum(o => o.TotalAmount),
                    completedOrders.Where(o => o.CompletedAt.HasValue).Max(o => (DateTime?)o.CompletedAt));
            })
            .ToList();
        return Task.FromResult<(IReadOnlyList<CustomerSearchResult>, int)>((items, all.Count));
    }

    public Task<(IReadOnlyList<CustomerLoyaltyPoint> Items, int TotalCount)> GetLoyaltyPointsAsync(CustomerLoyaltyPointSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var all = PointEntries.Where(p => p.CustomerId == criteria.CustomerId).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToList();
        return Task.FromResult<(IReadOnlyList<CustomerLoyaltyPoint>, int)>((all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList(), all.Count));
    }

    public Task<(IReadOnlyList<CustomerPurchasedItemDto> Items, int TotalCount)> GetPurchasedItemsAsync(CustomerPurchasedItemSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var all = PurchasedItems.Where(p => p.CustomerId == criteria.CustomerId).Select(p => p.Item)
            .OrderByDescending(p => p.CompletedAt).ThenByDescending(p => p.OrderId).ThenByDescending(p => p.Id).ToList();
        return Task.FromResult<(IReadOnlyList<CustomerPurchasedItemDto>, int)>((all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList(), all.Count));
    }

    public Task<bool> PhoneExistsAsync(string phone, Guid? excludingCustomerId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Customers.Values.Any(c => c.Phone == phone && c.Id != excludingCustomerId));

    public void Add(Customer customer) => Customers[customer.Id] = customer;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Customers.TryGetValue(id, out var customer);
        return Task.FromResult(customer);
    }
}
