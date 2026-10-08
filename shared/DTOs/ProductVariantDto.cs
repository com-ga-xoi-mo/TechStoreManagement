using System.Text.Json;

namespace TechStore.Shared.DTOs;

public class ProductVariantDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public Dictionary<string, JsonElement> Specs { get; set; } = [];
    public Dictionary<string, JsonElement> EffectiveSpecs { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
