using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface IInventoryAfterSaleRepository
{
    Task<Order?> LockOrderAsync(Guid orderId, CancellationToken ct);
    Task<InventoryOperation?> LockOperationAsync(Guid operationId, CancellationToken ct);
    Task<Dictionary<Guid, InventoryReservation>> GetReservationsAsync(IEnumerable<Guid> itemIds, CancellationToken ct);
    Task<IReadOnlyList<OrderItem>> GetLegacyHeldItemsAsync(Guid[] itemIds, CancellationToken ct);
    Task<Guid?> FindLegacySaleAsync(Guid deviceId, CancellationToken ct);
    Task<bool> HasInspectionAsync(Guid returnId, CancellationToken ct);
    Task<bool> HasPendingBankReturnAsync(Guid orderId, Guid[] orderItemIds, Guid? exceptId, CancellationToken ct);
    void Add(InventoryOperation operation);
    void Add(InventoryReservation reservation);
}
