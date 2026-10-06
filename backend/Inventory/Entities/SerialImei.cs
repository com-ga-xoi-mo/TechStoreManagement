using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Entities;

public class SerialImei : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Imei { get; set; }
    public SerialImeiStatus Status { get; set; } = SerialImeiStatus.InStock;
    public decimal? UnitCost { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public int WarrantyMonths { get; set; } = 12;
    public DateTime? WarrantyStartAt { get; set; }
    public DateTime? WarrantyEndAt { get; set; }

    // Navigation properties
    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? Variant { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; }
}
