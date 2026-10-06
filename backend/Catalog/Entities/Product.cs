using TechStore.Api.Common.Entities;

namespace TechStore.Api.Catalog.Entities;

public class Product : BaseEntity
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Brand { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal CostPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Specs { get; set; } = "{}";
    public bool IsSerialTracked { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
