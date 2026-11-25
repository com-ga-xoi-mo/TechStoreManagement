using System.Text.Json;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Services;

// Field order and enum encoding form part of the persisted payload used when a caller retries.
internal static class InventoryOperationLog
{
    public static InventoryOperation OrderLines(Guid id, bool release, Guid orderId, IReadOnlyList<FulfillmentLine> lines, DateTime at) =>
        Entry(id, release ? "Release" : "ReceiveReturn", new { orderId, lines = lines.OrderBy(l => l.OrderItemId) }, at);

    public static InventoryOperation Inspection(Guid id, Guid orderReturnId, FulfillmentLine line,
        ReturnDisposition disposition, Guid actorId, DateTime at) =>
        Entry(id, "Inspect", new { orderReturnId, line, disposition, actorId }, at);

    public static InventoryOperation Warranty(Guid id, Guid caseId, Guid orderId, Guid orderItemId, Guid deviceId,
        WarrantyAction action, Guid? replacementId, Guid actorId, DateTime at) =>
        Entry(id, "Warranty", new { caseId, orderId, orderItemId, deviceId, action, replacementId, actorId }, at);

    private static InventoryOperation Entry(Guid id, string kind, object payload, DateTime at) =>
        new() { Id = id, Kind = kind, Payload = JsonSerializer.Serialize(payload), CreatedAt = at };
}
