using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface ISerialImeiRepository
{
    Task<(IReadOnlyList<SerialImei> Items, int TotalCount)> SearchAsync(
        SerialImeiSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<ExistingIdentifiersResult> FindExistingIdentifiersAsync(
        IEnumerable<string> serialNumbers,
        IEnumerable<string> imeis,
        CancellationToken cancellationToken = default);

    // Read-only lookup of devices by id; ids that do not exist are simply absent from the result.
    Task<IReadOnlyList<SerialImei>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    // Tracked devices locked with SELECT ... FOR UPDATE in ascending id order.
    // Requires an open transaction opened by the calling service; outside one the lock is released immediately.
    Task<IReadOnlyList<SerialImei>> GetForUpdateAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SerialImei>> GetByStockKeyForUpdateAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<SerialImei> items);

    void RemoveRange(IEnumerable<SerialImei> items);
}
