namespace TechStore.Api.Inventory.Services;

// One order line as seen by Inventory. A serial-tracked line carries the device it sells
// (SerialImeiId set, Quantity 1); a non-tracked line has no device and a quantity of units.
// OrderItemId is required by ConfirmSaleAsync (the movement references the order line).
public record FulfillmentLine(
    Guid? OrderItemId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SerialImeiId,
    int Quantity);

public enum FulfillmentProblemKind
{
    Device,
    StockNotInitialized,
    InsufficientStock
}

public record FulfillmentProblem(
    int LineIndex,
    string Message,
    FulfillmentProblemKind Kind = FulfillmentProblemKind.Device);

public record FulfillmentCheckResult(IReadOnlyList<FulfillmentProblem> Problems)
{
    public bool IsAvailable => Problems.Count == 0;
}

public record FulfillmentResult(bool IsSuccess, FulfillmentProblem? Conflict)
{
    public static FulfillmentResult Success() => new(true, null);

    public static FulfillmentResult Failed(FulfillmentProblem conflict) => new(false, conflict);
}

// UnitCosts maps an order item id to the cost to snapshot on that order line: the sold device's own
// unit_cost for a serial-tracked line, the average received purchase cost for a non-tracked line
// (null when none is known). products.cost_price is only a reference and is never used.
public record SaleConfirmationResult(
    bool IsSuccess,
    FulfillmentProblem? Conflict,
    IReadOnlyDictionary<Guid, decimal?> UnitCosts)
{
    public static SaleConfirmationResult Success(IReadOnlyDictionary<Guid, decimal?> unitCosts) =>
        new(true, null, unitCosts);

    public static SaleConfirmationResult Failed(FulfillmentProblem conflict) =>
        new(false, conflict, new Dictionary<Guid, decimal?>());
}

// Inventory-owned operations that sell devices and stock for an order.
// The mutating operations only STAGE changes on the shared unit of work: they never save, begin,
// commit or roll back a transaction. The caller (OrderService) opens the transaction before calling
// them and owns SaveChanges / Commit / Rollback.
public interface IInventoryFulfillmentService
{
    // Read-only, no locks: one problem per failing line (keyed by line index).
    Task<FulfillmentCheckResult> CheckAvailabilityAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken = default);

    // Locks devices (ascending id) then stock rows (ascending product, variant), verifies availability and stages
    // device InStock -> Reserved and stock.reserved_quantity += quantity. No movement is written.
    Task<FulfillmentResult> ReserveAsync(
        IReadOnlyList<FulfillmentLine> lines,
        CancellationToken cancellationToken = default);

    // Requires the devices to be Reserved (and non-tracked quantities covered by reserved_quantity). Stages device
    // Reserved -> Sold with warranty dates, quantity and reserved_quantity reduced, and one Sale movement per line.
    Task<SaleConfirmationResult> ConfirmSaleAsync(
        IReadOnlyList<FulfillmentLine> lines,
        DateTime completedAt,
        Guid performedByUserId,
        CancellationToken cancellationToken = default);
}
