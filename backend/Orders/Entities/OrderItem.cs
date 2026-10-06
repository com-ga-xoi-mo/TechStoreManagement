using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Entities;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Orders.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? SerialImeiId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalPrice { get; set; }

    // Navigation properties
    public virtual Order Order { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? Variant { get; set; }
    public virtual SerialImei? SerialImei { get; set; }
}
