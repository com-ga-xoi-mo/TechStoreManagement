namespace TechStore.Api.Inventory.Services;

public enum ReturnDisposition { Restock, Defective, UnderRepair }
public enum WarrantyAction { ReceiveForRepair, ReturnToCustomer, Replace }

public record InventoryOperationResult(bool IsSuccess, FulfillmentProblem? Conflict = null, Guid? MovementId = null, decimal? UnitCost = null)
{
    internal static InventoryOperationResult Failed(string message, int line = 0) =>
        new(false, new FulfillmentProblem(line, message));
}

public interface IInventoryAfterSaleService
{
    Task<InventoryOperationResult> CheckReleaseAsync(Guid orderId, IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default);
    Task<InventoryOperationResult> CheckReturnAsync(Guid orderId, IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default);
    Task<InventoryOperationResult> ReleaseAsync(Guid operationId, Guid orderId,
        IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default);
    Task<InventoryOperationResult> ReceiveReturnAsync(Guid operationId, Guid orderId,
        IReadOnlyList<FulfillmentLine> lines, CancellationToken ct = default);
    Task<InventoryOperationResult> InspectAsync(Guid inspectionId, Guid orderReturnId, FulfillmentLine line,
        ReturnDisposition disposition, Guid actorId, CancellationToken ct = default);
    Task<InventoryOperationResult> ApplyWarrantyAsync(Guid operationId, Guid caseId, Guid orderId,
        Guid orderItemId, Guid deviceId, WarrantyAction action, Guid? replacementId, Guid actorId, CancellationToken ct = default);
}
