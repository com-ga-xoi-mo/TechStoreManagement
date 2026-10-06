using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Entities;

namespace TechStore.Api.Inventory.Entities;

public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; } = 0;
    public decimal UnitCost { get; set; }

    // Navigation properties
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? Variant { get; set; }
}
