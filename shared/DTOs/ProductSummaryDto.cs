namespace TechStore.Shared.DTOs;

public class ProductSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal PriceFrom { get; set; }
    public decimal PriceTo { get; set; }
    public int VariantCount { get; set; }
    public bool IsSerialTracked { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
}
