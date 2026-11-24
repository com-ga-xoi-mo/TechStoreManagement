using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Services;

public class InventoryAfterSaleService(IInventoryAfterSaleRepository repository, ISerialImeiRepository serials,
    IInventoryStockRepository stocks, TimeProvider time) : IInventoryAfterSaleService
{
    public Task<InventoryOperationResult> CheckReleaseAsync(Guid orderId, IReadOnlyList<FulfillmentLine> lines,
        CancellationToken ct = default) => ProcessLinesAsync(null, orderId, lines, release: true, ct);

    public Task<InventoryOperationResult> CheckReturnAsync(Guid orderId, IReadOnlyList<FulfillmentLine> lines,
        CancellationToken ct = default) => ProcessLinesAsync(null, orderId, lines, release: false, ct);

    public Task<InventoryOperationResult> ReleaseAsync(Guid operationId, Guid orderId,
        IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default) =>
        ProcessLinesAsync(operationId, orderId, lines, release: true, ct);

    public Task<InventoryOperationResult> ReceiveReturnAsync(Guid operationId, Guid orderId,
        IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default) =>
        ProcessLinesAsync(operationId, orderId, lines, release: false, ct);

    private async Task<InventoryOperationResult> ProcessLinesAsync(Guid? operationId, Guid orderId,
        IReadOnlyList<FulfillmentLine> lines, bool release, CancellationToken ct)
    {
        var order = await repository.LockOrderAsync(orderId, ct);
        if (order == null)
            return InventoryOperationResult.Failed("Order was not found.");

        var operation = InventoryOperationLog.OrderLines(operationId.GetValueOrDefault(), release, orderId, lines, time.GetUtcNow().UtcDateTime);
        if (operationId.HasValue && await ReplayAsync(operation, ct) is { } replay)
            return replay;

        if (lines.Count == 0 || lines.Any(l => !ValidLine(l)) ||
            lines.Select(l => l.OrderItemId).Distinct().Count() != lines.Count ||
            lines.Where(l => l.SerialImeiId.HasValue).GroupBy(l => l.SerialImeiId).Any(g => g.Count() > 1))
            return InventoryOperationResult.Failed("Each line must identify distinct units of an order.");

        if (release && (order.Status is not (OrderStatus.Pending or OrderStatus.Processing) || order.Items.Count != lines.Count))
            return InventoryOperationResult.Failed("Only a complete outstanding order reservation can be released.");
        if (!release && order.Status is not (OrderStatus.Completed or OrderStatus.PartiallyRefunded))
            return InventoryOperationResult.Failed("Only delivered goods may be received.");

        var itemIds = lines.Select(l => l.OrderItemId!.Value).ToArray();
        if (await repository.HasPendingBankReturnAsync(orderId, itemIds, operationId, ct))
            return InventoryOperationResult.Failed("These units are held for another bank refund.");

        var byId = order.Items.ToDictionary(i => i.Id);
        var devices = (await serials.GetForUpdateAsync(lines.Where(l => l.SerialImeiId.HasValue)
            .Select(l => l.SerialImeiId!.Value), ct)).ToDictionary(d => d.Id);
        var holds = release ? await repository.GetReservationsAsync(itemIds, ct) : [];
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!byId.TryGetValue(line.OrderItemId!.Value, out var item) ||
                item.ProductId != line.ProductId || item.VariantId != line.VariantId || item.SerialImeiId != line.SerialImeiId ||
                (release ? line.Quantity != item.Quantity : line.Quantity > item.Quantity))
                return InventoryOperationResult.Failed("The units do not belong to this order line.", i);

            if (release)
            {
                if (holds.TryGetValue(item.Id, out var hold))
                {
                    if (hold.State != ReservationState.Held || hold.Quantity != line.Quantity)
                        return InventoryOperationResult.Failed("This reservation has already been consumed.", i);
                }
                else if (order.Status != OrderStatus.Processing)
                    return InventoryOperationResult.Failed("This order line has no active reservation.", i);
            }

            if (line.SerialImeiId is not Guid id)
                continue;
            if (!devices.TryGetValue(id, out var device) || device.ProductId != item.ProductId || device.VariantId != item.VariantId ||
                device.Status != (release ? SerialImeiStatus.Reserved : SerialImeiStatus.Sold) ||
                (!release && !await OwnsSaleAsync(device, item.Id, ct)))
                return InventoryOperationResult.Failed("Device is no longer held or sold to the specified order line.", i);
        }

        var quantities = lines.GroupBy(l => new StockLockKey(l.ProductId, l.VariantId))
            .ToDictionary(g => g.Key, g => g.Sum(l => (long)l.Quantity));
        var lockedStocks = new Dictionary<StockLockKey, InventoryStock>();
        if (release)
        {
            foreach (var key in InventoryLockOrdering.OrderKeys(quantities.Keys))
            {
                var stock = await stocks.GetForUpdateAsync(key.ProductId, key.VariantId, ct);
                if (stock == null || stock.ReservedQuantity < quantities[key])
                    return InventoryOperationResult.Failed("Reserved stock no longer covers the order.",
                        lines.ToList().FindIndex(l => l.ProductId == key.ProductId && l.VariantId == key.VariantId));
                lockedStocks[key] = stock;
            }
        }

        if (!operationId.HasValue) return new(true);

        foreach (var line in lines)
        {
            if (release)
            {
                if (!holds.TryGetValue(line.OrderItemId!.Value, out var hold))
                {
                    // M3 persisted Processing and the immutable order items in the same transaction as reservation.
                    hold = new InventoryReservation { OrderItemId = line.OrderItemId.Value, Quantity = line.Quantity };
                    repository.Add(hold);
                }
                hold.State = ReservationState.Released;
            }
            if (line.SerialImeiId is Guid id)
            {
                var device = devices[id];
                device.Status = release ? SerialImeiStatus.InStock : SerialImeiStatus.Returned;
                device.AfterSaleQuarantine = !release;
                device.UpdatedAt = operation.CreatedAt;
            }
        }
        foreach (var (key, stock) in lockedStocks)
        {
            stock.ReservedQuantity -= (int)quantities[key];
            stock.UpdatedAt = operation.CreatedAt;
        }
        return Record(operation);
    }

    public async Task<InventoryOperationResult> InspectAsync(Guid inspectionId, Guid orderReturnId, FulfillmentLine line,
        ReturnDisposition disposition, Guid actorId, CancellationToken ct = default)
    {
        var operation = InventoryOperationLog.Inspection(inspectionId, orderReturnId, line, disposition, actorId, time.GetUtcNow().UtcDateTime);
        if (await ReplayAsync(operation, ct) is { } replay)
            return replay;
        if (orderReturnId == Guid.Empty || actorId == Guid.Empty || !ValidLine(line) || !Enum.IsDefined(disposition))
            return InventoryOperationResult.Failed("The inspection line or disposition is invalid.");
        if (await repository.HasInspectionAsync(orderReturnId, ct))
            return InventoryOperationResult.Failed("This returned quantity has already been inspected.");

        SerialImei? device = null;
        if (line.SerialImeiId is Guid id)
        {
            device = (await serials.GetForUpdateAsync([id], ct)).SingleOrDefault();
            if (device?.Status != SerialImeiStatus.Returned || device.ProductId != line.ProductId ||
                device.VariantId != line.VariantId || !await OwnsSaleAsync(device, line.OrderItemId!.Value, ct))
                return InventoryOperationResult.Failed("This device is not awaiting inspection for the sale.");
        }

        if (disposition == ReturnDisposition.Restock)
        {
            var stock = await stocks.GetForUpdateAsync(line.ProductId, line.VariantId, ct);
            if (stock == null || stock.Quantity > int.MaxValue - line.Quantity)
                return InventoryOperationResult.Failed("The stock row cannot receive this quantity.");
            var movement = Move(stock, line.Quantity, InventoryMovementType.CustomerReturn, actorId, operation.CreatedAt);
            movement.OrderReturnId = orderReturnId;
            operation.MovementId = movement.Id;
        }
        if (device != null)
        {
            device.Status = disposition switch
            {
                ReturnDisposition.Restock => SerialImeiStatus.InStock,
                ReturnDisposition.Defective => SerialImeiStatus.Defective,
                _ => SerialImeiStatus.UnderRepair
            };
            device.AfterSaleQuarantine = disposition != ReturnDisposition.Restock;
            device.UpdatedAt = operation.CreatedAt;
        }
        operation.InspectionReturnId = orderReturnId;
        return Record(operation);
    }

    public async Task<InventoryOperationResult> ApplyWarrantyAsync(Guid operationId, Guid caseId, Guid orderId,
        Guid orderItemId, Guid deviceId, WarrantyAction action, Guid? replacementId, Guid actorId, CancellationToken ct = default)
    {
        var order = await repository.LockOrderAsync(orderId, ct);
        if (order == null)
            return InventoryOperationResult.Failed("Order was not found.");
        var operation = InventoryOperationLog.Warranty(operationId, caseId, orderId, orderItemId, deviceId, action, replacementId, actorId, time.GetUtcNow().UtcDateTime);
        if (await ReplayAsync(operation, ct) is { } replay)
            return replay;
        if (caseId == Guid.Empty || actorId == Guid.Empty || !Enum.IsDefined(action) || deviceId == Guid.Empty ||
            (action == WarrantyAction.Replace) != replacementId.HasValue || replacementId == deviceId)
            return InventoryOperationResult.Failed("Invalid warranty decision or replacement device.");
        var item = order.Items.SingleOrDefault(i => i.Id == orderItemId);
        if (item?.SerialImeiId == null || order.Status is not (OrderStatus.Completed or OrderStatus.PartiallyRefunded) ||
            await repository.HasPendingBankReturnAsync(orderId, [orderItemId], null, ct))
            return InventoryOperationResult.Failed("This order line is unavailable for warranty service.");

        var ids = replacementId.HasValue ? new[] { deviceId, replacementId.Value } : [deviceId];
        var devices = (await serials.GetForUpdateAsync(ids, ct)).ToDictionary(d => d.Id);
        if (!devices.TryGetValue(deviceId, out var device) || device.ProductId != item.ProductId ||
            device.VariantId != item.VariantId || !await OwnsSaleAsync(device, orderItemId, ct))
            return InventoryOperationResult.Failed("The device does not belong to this sale.");

        bool customerMachine = device.Status == SerialImeiStatus.Sold && device.WarrantyCaseId == null;
        bool inThisRepair = device.Status == SerialImeiStatus.UnderRepair && device.WarrantyCaseId == caseId;
        if (!(action switch
        {
            WarrantyAction.ReceiveForRepair => customerMachine,
            WarrantyAction.ReturnToCustomer => inThisRepair,
            WarrantyAction.Replace => customerMachine || inThisRepair,
            _ => false
        }))
            return InventoryOperationResult.Failed("The device state or warranty case does not match.");

        if (action == WarrantyAction.Replace)
        {
            if (!devices.TryGetValue(replacementId!.Value, out var replacement) || replacement.Status != SerialImeiStatus.InStock ||
                replacement.ProductId != device.ProductId || replacement.VariantId != device.VariantId)
                return InventoryOperationResult.Failed("Replacement must be an available device of the same product and variant.");
            var stock = await stocks.GetForUpdateAsync(item.ProductId, item.VariantId, ct);
            if (stock == null || stock.Quantity <= stock.ReservedQuantity)
                return InventoryOperationResult.Failed("The replacement unit is unavailable.");

            var movement = Move(stock, -1, InventoryMovementType.WarrantyReplacement, actorId, operation.CreatedAt);
            movement.OrderItemId = orderItemId;
            operation.MovementId = movement.Id;
            operation.UnitCost = replacement.UnitCost;
            replacement.SaleOrderItemId = orderItemId;
            replacement.WarrantyStartAt = device.WarrantyStartAt;
            replacement.WarrantyEndAt = device.WarrantyEndAt;
            replacement.Status = SerialImeiStatus.Sold;
            replacement.UpdatedAt = operation.CreatedAt;
            device.Status = SerialImeiStatus.Defective;
            device.AfterSaleQuarantine = true;
        }
        else
        {
            device.Status = action == WarrantyAction.ReceiveForRepair ? SerialImeiStatus.UnderRepair : SerialImeiStatus.Sold;
            device.AfterSaleQuarantine = action == WarrantyAction.ReceiveForRepair;
        }
        device.WarrantyCaseId = action == WarrantyAction.ReceiveForRepair ? caseId : null;
        device.UpdatedAt = operation.CreatedAt;
        return Record(operation);
    }

    private async Task<bool> OwnsSaleAsync(SerialImei device, Guid itemId, CancellationToken ct) =>
        (device.SaleOrderItemId ?? await repository.FindLegacySaleAsync(device.Id, ct)) == itemId;

    private InventoryMovement Move(InventoryStock stock, int change, InventoryMovementType type, Guid actorId, DateTime now)
    {
        stock.Quantity += change;
        stock.UpdatedAt = now;
        var movement = new InventoryMovement { InventoryStockId = stock.Id, QuantityChange = change, QuantityAfter = stock.Quantity,
            MovementType = type, PerformedByUserId = actorId, CreatedAt = now };
        stocks.AddMovement(movement);
        return movement;
    }

    private static bool ValidLine(FulfillmentLine line) =>
        line.OrderItemId.HasValue && line.OrderItemId != Guid.Empty && line.ProductId != Guid.Empty &&
        line.Quantity > 0 && (!line.SerialImeiId.HasValue || line.Quantity == 1);

    private async Task<InventoryOperationResult?> ReplayAsync(InventoryOperation next, CancellationToken ct)
    {
        if (next.Id == Guid.Empty) return InventoryOperationResult.Failed("An operation ID is required.");
        var prior = await repository.LockOperationAsync(next.Id, ct);
        if (prior == null) return null;
        if (prior.Kind != next.Kind || prior.Payload != next.Payload)
            return InventoryOperationResult.Failed("The operation ID was already used with a different payload.");
        return new(true, MovementId: prior.MovementId, UnitCost: prior.UnitCost);
    }

    private InventoryOperationResult Record(InventoryOperation operation)
    {
        repository.Add(operation);
        return new(true, MovementId: operation.MovementId, UnitCost: operation.UnitCost);
    }
}
