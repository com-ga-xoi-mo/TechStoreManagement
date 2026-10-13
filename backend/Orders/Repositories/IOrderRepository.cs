using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Repositories;

public interface IOrderRepository
{
    // Read-only page of orders with Customer and Items loaded (Items are used for the line count).
    Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    // Read-only order with Customer, Items and each item's SerialImei.
    Task<Order?> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Tracked order locked with SELECT ... FOR UPDATE, with Items (Product, Variant, SerialImei) loaded.
    // Requires an open transaction opened by the calling service; outside one the lock is released immediately.
    Task<Order?> GetForUpdateWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Highest existing order code that starts with the given prefix, or null.
    Task<string?> GetLastOrderCodeAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    void Add(Order order);
}
