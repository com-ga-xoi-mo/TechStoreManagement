using TechStore.Api.Common.Entities;

namespace TechStore.Api.Catalog.Entities;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public string Specs { get; set; } = "{}";
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Product Product { get; set; } = null!;
}
