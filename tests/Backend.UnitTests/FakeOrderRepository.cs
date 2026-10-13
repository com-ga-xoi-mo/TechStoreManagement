using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;

namespace TechStore.UnitTests;

public class FakeOrderRepository : IOrderRepository
{
    public Dictionary<Guid, Order> Orders { get; } = new();
    public List<Order> AddedOrders { get; } = [];

    // Highest existing order code returned by GetLastOrderCodeAsync (null = none).
    public string? LastOrderCode { get; set; }

    // Raised once per call; lets a test simulate a code collision on the next save.
    public int GetLastOrderCodeCallCount { get; private set; }

    public int SearchCallCount { get; private set; }
    public OrderSearchCriteria? LastCriteria { get; private set; }
    public (IReadOnlyList<Order> Items, int TotalCount)? CustomSearchResult { get; set; }

    public int GetForUpdateCallCount { get; private set; }

    public Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        SearchCallCount++;
        LastCriteria = criteria;

        if (CustomSearchResult.HasValue)
        {
            return Task.FromResult(CustomSearchResult.Value);
        }

        var items = Orders.Values.ToList();
        return Task.FromResult<(IReadOnlyList<Order> Items, int TotalCount)>((items, items.Count));
    }

    public Task<Order?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Orders.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<Order?> GetForUpdateWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetForUpdateCallCount++;
        Orders.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<string?> GetLastOrderCodeAsync(string prefix, CancellationToken cancellationToken = default)
    {
        GetLastOrderCodeCallCount++;
        var existing = Orders.Values
            .Concat(AddedOrders)
            .Select(o => o.OrderCode)
            .Where(c => c.StartsWith(prefix, StringComparison.Ordinal))
            .OrderByDescending(c => c.Length)
            .ThenByDescending(c => c)
            .FirstOrDefault();

        if (LastOrderCode != null && LastOrderCode.StartsWith(prefix, StringComparison.Ordinal))
        {
            existing = existing == null || string.CompareOrdinal(LastOrderCode, existing) > 0 ? LastOrderCode : existing;
        }

        return Task.FromResult(existing);
    }

    public void Add(Order order)
    {
        AddedOrders.Add(order);
        Orders[order.Id] = order;
    }
}
