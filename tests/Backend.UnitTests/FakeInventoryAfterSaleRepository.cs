using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Orders.Entities;

namespace TechStore.UnitTests;

public class FakeInventoryAfterSaleRepository : IInventoryAfterSaleRepository
{
    public List<InventoryReservation> Reservations { get; } = [];
    public List<InventoryOperation> Operations { get; } = [];
    public List<Order> Orders { get; } = [];
    public bool IsAwaitingBank { get; set; }
    public Task<Order?> LockOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(Orders.SingleOrDefault(o => o.Id == orderId));
    public Task<InventoryOperation?> LockOperationAsync(Guid operationId, CancellationToken ct) =>
        Task.FromResult(Operations.SingleOrDefault(x => x.Id == operationId));
    public Task<Dictionary<Guid, InventoryReservation>> GetReservationsAsync(IEnumerable<Guid> itemIds, CancellationToken ct) =>
        Task.FromResult(Reservations.Where(x => itemIds.Contains(x.OrderItemId)).ToDictionary(x => x.OrderItemId));
    public Task<IReadOnlyList<OrderItem>> GetLegacyHeldItemsAsync(Guid[] itemIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderItem>>(Orders.Where(o => o.Status == TechStore.Shared.Enums.OrderStatus.Processing)
            .SelectMany(o => o.Items).Where(i => itemIds.Contains(i.Id)).ToList());
    public Task<Guid?> FindLegacySaleAsync(Guid deviceId, CancellationToken ct) =>
        Task.FromResult(Orders.Where(o => o.CompletedAt.HasValue).OrderByDescending(o => o.CompletedAt)
            .SelectMany(o => o.Items).FirstOrDefault(i => i.SerialImeiId == deviceId)?.Id);
    public Task<bool> HasPendingBankReturnAsync(Guid orderId, Guid[] orderItemIds, Guid? exceptId, CancellationToken ct) => Task.FromResult(IsAwaitingBank);
    public Task<bool> HasInspectionAsync(Guid returnId, CancellationToken ct) => Task.FromResult(Operations.Any(x => x.InspectionReturnId == returnId));
    public void Add(InventoryReservation reservation) => Reservations.Add(reservation);
    public void Add(InventoryOperation operation) => Operations.Add(operation);
}
