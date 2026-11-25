using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Repositories;

public class InventoryAfterSaleRepository(AppDbContext db) : IInventoryAfterSaleRepository
{
    public async Task<Order?> LockOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders WHERE id = {orderId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (order != null)
            await db.OrderItems.FromSqlInterpolated($"SELECT * FROM order_items WHERE order_id = {orderId} ORDER BY id FOR UPDATE").LoadAsync(ct);
        return order;
    }

    public async Task<InventoryOperation?> LockOperationAsync(Guid operationId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Inventory after-sale operations require a caller-owned transaction.");
        // A new operation has no row to lock. Serializing its key also protects simultaneous first use on different orders.
        var key = BitConverter.ToInt64(operationId.ToByteArray());
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        var operations = db.Set<InventoryOperation>();
        return operations.Local.FirstOrDefault(x => x.Id == operationId) ??
            await operations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, ct);
    }

    public async Task<Dictionary<Guid, InventoryReservation>> GetReservationsAsync(IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToArray();
        var reservations = db.Set<InventoryReservation>();
        await reservations.Where(x => ids.Contains(x.OrderItemId)).LoadAsync(ct);
        // POS can reserve and sell before the caller first saves its new order.
        return reservations.Local.Where(x => ids.Contains(x.OrderItemId)).ToDictionary(x => x.OrderItemId);
    }

    public async Task<IReadOnlyList<OrderItem>> GetLegacyHeldItemsAsync(Guid[] itemIds, CancellationToken ct) =>
        await db.OrderItems.AsNoTracking().Where(i => itemIds.Contains(i.Id) && i.Order.Status == OrderStatus.Processing)
            .ToListAsync(ct);

    public Task<Guid?> FindLegacySaleAsync(Guid deviceId, CancellationToken ct) =>
        db.OrderItems.AsNoTracking().Where(x => x.SerialImeiId == deviceId && x.Order.CompletedAt != null)
            .OrderByDescending(x => x.Order.CompletedAt).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);

    public Task<bool> HasInspectionAsync(Guid returnId, CancellationToken ct) =>
        db.Set<InventoryOperation>().Local.Any(x => x.InspectionReturnId == returnId)
            ? Task.FromResult(true)
            : db.Set<InventoryOperation>().AsNoTracking().AnyAsync(x => x.InspectionReturnId == returnId, ct);

    public Task<bool> HasPendingBankReturnAsync(Guid orderId, Guid[] orderItemIds, Guid? exceptId, CancellationToken ct) =>
        db.Set<ReturnCase>().AsNoTracking().AnyAsync(x => x.OrderId == orderId && x.Id != exceptId &&
            x.Status == ReturnStatus.AwaitingBank && x.Lines.Any(l => orderItemIds.Contains(l.OrderItemId)), ct);

    public void Add(InventoryOperation operation) => db.Add(operation);
    public void Add(InventoryReservation reservation) => db.Add(reservation);
}
