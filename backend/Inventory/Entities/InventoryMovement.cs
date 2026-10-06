using TechStore.Api.Common.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Entities;

public class InventoryMovement : BaseEntity
{
    public Guid InventoryStockId { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public int QuantityChange { get; set; }
    public int QuantityAfter { get; set; }
    public Guid PerformedByUserId { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid? OrderReturnId { get; set; }
    public string? Reason { get; set; }

    // Navigation properties
    public virtual InventoryStock InventoryStock { get; set; } = null!;
    public virtual User PerformedByUser { get; set; } = null!;
    public virtual PurchaseOrderItem? PurchaseOrderItem { get; set; }
}
