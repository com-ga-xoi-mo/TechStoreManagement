using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechStore.Api.Inventory.Entities;

public class InventoryStock : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 0;
    public int ReservedQuantity { get; set; } = 0;
    public int MinStockAlert { get; set; } = 5;

    // Read-only presentation metadata populated by the inventory repository.
    // It is not persisted on inventory_stocks.
    [NotMapped]
    public int? RelatedSerialCount { get; set; }

    // Navigation properties
    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? Variant { get; set; }
    public virtual ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
