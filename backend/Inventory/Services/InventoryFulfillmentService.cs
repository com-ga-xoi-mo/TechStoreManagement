using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Services;

public class InventoryFulfillmentService(
    ISerialImeiRepository serialImeiRepository,
    IInventoryStockRepository stockRepository) : IInventoryFulfillmentService
{
    private readonly record struct StockKey(Guid ProductId, Guid? VariantId);

    public async Task<FulfillmentCheckResult> CheckAvailabilityAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken = default)
    {
        var problems = new List<FulfillmentProblem>();
        var devices = await LoadDevicesAsync(lines, forUpdate: false, cancellationToken);
        var stocks = new Dictionary<StockKey, InventoryStock?>();
        var demand = new Dictionary<StockKey, int>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.SerialImeiId.HasValue)
            {
                var deviceProblem = ValidateDevice(line, devices, SerialImeiStatus.InStock);
                if (deviceProblem != null)
                {
                    problems.Add(new FulfillmentProblem(i, deviceProblem));
                    continue;
                }
            }

            var key = new StockKey(line.ProductId, line.VariantId);
            if (!stocks.TryGetValue(key, out var stock))
            {
                stock = await stockRepository.GetByProductAndVariantAsync(key.ProductId, key.VariantId, cancellationToken);
                stocks[key] = stock;
            }

            if (stock == null)
            {
                problems.Add(new FulfillmentProblem(i, StockNotInitializedMessage, FulfillmentProblemKind.StockNotInitialized));
                continue;
            }

            var total = demand.GetValueOrDefault(key) + line.Quantity;
            var available = stock.Quantity - stock.ReservedQuantity;
            if (total > available)
            {
                problems.Add(new FulfillmentProblem(i, InsufficientStockMessage(available), FulfillmentProblemKind.InsufficientStock));
                continue;
            }

            demand[key] = total;
        }

        return new FulfillmentCheckResult(problems);
    }

    public async Task<FulfillmentResult> ReserveAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken = default)
    {
        // Lock order: devices (ascending id), then stock rows (ascending product, variant).
        var devices = await LoadDevicesAsync(lines, forUpdate: true, cancellationToken);

        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].SerialImeiId.HasValue)
            {
                continue;
            }

            var deviceProblem = ValidateDevice(lines[i], devices, SerialImeiStatus.InStock);
            if (deviceProblem != null)
            {
                return FulfillmentResult.Failed(new FulfillmentProblem(i, deviceProblem));
            }
        }

        var (stocks, stockProblem) = await LockStocksAsync(lines, cancellationToken);
        if (stockProblem != null)
        {
            return FulfillmentResult.Failed(stockProblem);
        }

        var demand = new Dictionary<StockKey, int>();
        for (var i = 0; i < lines.Count; i++)
        {
            var key = new StockKey(lines[i].ProductId, lines[i].VariantId);
            var stock = stocks[key];
            var total = demand.GetValueOrDefault(key) + lines[i].Quantity;
            var available = stock.Quantity - stock.ReservedQuantity;
            if (total > available)
            {
                return FulfillmentResult.Failed(new FulfillmentProblem(i, InsufficientStockMessage(available), FulfillmentProblemKind.InsufficientStock));
            }

            demand[key] = total;
        }

        // Everything verified: stage the changes.
        var now = DateTime.UtcNow;
        foreach (var line in lines.Where(l => l.SerialImeiId.HasValue))
        {
            var device = devices[line.SerialImeiId!.Value];
            device.Status = SerialImeiStatus.Reserved;
            device.UpdatedAt = now;
        }

        foreach (var (key, quantity) in demand)
        {
            var stock = stocks[key];
            stock.ReservedQuantity += quantity;
            stock.UpdatedAt = now;
        }

        return FulfillmentResult.Success();
    }

    public async Task<SaleConfirmationResult> ConfirmSaleAsync(
        IReadOnlyList<FulfillmentLine> lines,
        DateTime completedAt,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Any(l => !l.OrderItemId.HasValue))
        {
            throw new ArgumentException("Every line must carry its order item id.", nameof(lines));
        }

        var devices = await LoadDevicesAsync(lines, forUpdate: true, cancellationToken);

        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].SerialImeiId.HasValue)
            {
                continue;
            }

            var deviceProblem = ValidateDevice(lines[i], devices, SerialImeiStatus.Reserved);
            if (deviceProblem != null)
            {
                return SaleConfirmationResult.Failed(new FulfillmentProblem(i, deviceProblem));
            }
        }

        var (stocks, stockProblem) = await LockStocksAsync(lines, cancellationToken);
        if (stockProblem != null)
        {
            return SaleConfirmationResult.Failed(stockProblem);
        }

        var demand = new Dictionary<StockKey, int>();
        for (var i = 0; i < lines.Count; i++)
        {
            var key = new StockKey(lines[i].ProductId, lines[i].VariantId);
            var stock = stocks[key];
            var total = demand.GetValueOrDefault(key) + lines[i].Quantity;
            if (total > stock.ReservedQuantity || total > stock.Quantity)
            {
                return SaleConfirmationResult.Failed(new FulfillmentProblem(i, "Stock is not reserved for this sale.", FulfillmentProblemKind.InsufficientStock));
            }

            demand[key] = total;
        }

        // Everything verified: stage the changes in line order so quantity_after is monotonic per stock row.
        var unitCosts = new Dictionary<Guid, decimal?>();
        foreach (var line in lines)
        {
            decimal? unitCost;
            if (line.SerialImeiId.HasValue)
            {
                var device = devices[line.SerialImeiId.Value];
                device.Status = SerialImeiStatus.Sold;
                device.WarrantyStartAt = completedAt;
                device.WarrantyEndAt = completedAt.AddMonths(device.WarrantyMonths);
                device.UpdatedAt = completedAt;
                unitCost = device.UnitCost;
            }
            else
            {
                // No device of its own: cost is what the received purchase lines paid, not products.cost_price.
                unitCost = await stockRepository.GetAverageReceivedUnitCostAsync(line.ProductId, line.VariantId, cancellationToken);
            }

            var stock = stocks[new StockKey(line.ProductId, line.VariantId)];
            stock.Quantity -= line.Quantity;
            stock.ReservedQuantity -= line.Quantity;
            stock.UpdatedAt = completedAt;

            stockRepository.AddMovement(new InventoryMovement
            {
                InventoryStockId = stock.Id,
                MovementType = InventoryMovementType.Sale,
                QuantityChange = -line.Quantity,
                QuantityAfter = stock.Quantity,
                PerformedByUserId = performedByUserId,
                OrderItemId = line.OrderItemId
            });

            unitCosts[line.OrderItemId!.Value] = unitCost;
        }

        return SaleConfirmationResult.Success(unitCosts);
    }

    private const string StockNotInitializedMessage = "Stock is not initialized for this product or variant.";

    private static string InsufficientStockMessage(int available) =>
        $"Insufficient available stock ({Math.Max(available, 0)} available).";

    private async Task<Dictionary<Guid, SerialImei>> LoadDevicesAsync(
        IReadOnlyList<FulfillmentLine> lines,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var ids = lines
            .Where(l => l.SerialImeiId.HasValue)
            .Select(l => l.SerialImeiId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, SerialImei>();
        }

        var devices = forUpdate
            ? await serialImeiRepository.GetForUpdateAsync(ids, cancellationToken)
            : await serialImeiRepository.GetByIdsAsync(ids, cancellationToken);

        return devices.ToDictionary(d => d.Id);
    }

    // Locks the stock rows in ascending (product, variant) order and returns them by key.
    // Returns a problem (on the first line using the missing row) when a row does not exist.
    private async Task<(Dictionary<StockKey, InventoryStock> Stocks, FulfillmentProblem? Problem)> LockStocksAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken)
    {
        var stocks = new Dictionary<StockKey, InventoryStock>();
        var keys = lines
            .Select(l => new StockKey(l.ProductId, l.VariantId))
            .Distinct()
            .OrderBy(k => k.ProductId)
            .ThenBy(k => k.VariantId);

        foreach (var key in keys)
        {
            var stock = await stockRepository.GetForUpdateAsync(key.ProductId, key.VariantId, cancellationToken);
            if (stock == null)
            {
                var lineIndex = lines
                    .Select((line, index) => (line, index))
                    .First(x => x.line.ProductId == key.ProductId && x.line.VariantId == key.VariantId).index;
                return (stocks, new FulfillmentProblem(lineIndex, StockNotInitializedMessage, FulfillmentProblemKind.StockNotInitialized));
            }

            stocks[key] = stock;
        }

        return (stocks, null);
    }

    // Returns null when the device may be used for the line; otherwise a message.
    private static string? ValidateDevice(
        FulfillmentLine line,
        Dictionary<Guid, SerialImei> devices,
        SerialImeiStatus expectedStatus)
    {
        if (!devices.TryGetValue(line.SerialImeiId!.Value, out var device))
        {
            return "Device was not found.";
        }

        if (device.Status != expectedStatus)
        {
            return $"Device is {device.Status}; expected {expectedStatus}.";
        }

        // Guid? equality: a null variant only matches a null variant.
        if (device.ProductId != line.ProductId || device.VariantId != line.VariantId)
        {
            return "Device does not belong to the selected product or variant.";
        }

        return null;
    }
}
