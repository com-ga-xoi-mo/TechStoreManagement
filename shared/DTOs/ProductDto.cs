using System.Text.Json;

namespace TechStore.Shared.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Brand { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    /// <summary>Null when the caller's role may not see cost (see FieldAccess).</summary>
    public decimal? CostPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public List<ProductImageDto> Images { get; set; } = [];
    public Dictionary<string, JsonElement> Specs { get; set; } = [];
    public bool IsSerialTracked { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ProductVariantDto> Variants { get; set; } = [];
}
