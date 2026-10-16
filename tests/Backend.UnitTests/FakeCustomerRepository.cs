using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;

namespace TechStore.UnitTests;

public class FakeCustomerRepository : ICustomerRepository
{
    public Dictionary<Guid, Customer> Customers { get; } = new();
    public List<CustomerLoyaltyPoint> PointEntries { get; } = [];

    public Task<Customer?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        IEnumerable<Customer> query = Customers.Values;
        if (criteria.Query != null) query = query.Where(c => c.FullName.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase) || c.Phone.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase));
        if (criteria.Tier.HasValue) query = query.Where(c => c.Tier == criteria.Tier.Value);
        if (criteria.IsActive.HasValue) query = query.Where(c => c.IsActive == criteria.IsActive.Value);
        var all = query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).ToList();
        return Task.FromResult<(IReadOnlyList<Customer>, int)>((all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList(), all.Count));
    }

    public Task<(IReadOnlyList<CustomerLoyaltyPoint> Items, int TotalCount)> GetLoyaltyPointsAsync(CustomerLoyaltyPointSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var all = PointEntries.Where(p => p.CustomerId == criteria.CustomerId).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToList();
        return Task.FromResult<(IReadOnlyList<CustomerLoyaltyPoint>, int)>((all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList(), all.Count));
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
