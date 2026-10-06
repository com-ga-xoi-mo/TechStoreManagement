using TechStore.Api.Common.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Entities;

public class PurchaseOrder : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal TotalCost { get; set; } = 0;
    public string? Notes { get; set; }
    public DateTime? OrderedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    // Navigation properties
    public virtual Supplier Supplier { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual User? ReceivedByUser { get; set; }
    public virtual ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
