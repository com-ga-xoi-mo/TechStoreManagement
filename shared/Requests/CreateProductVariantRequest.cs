using System.Text.Json;

namespace TechStore.Shared.Requests;

public class CreateProductVariantRequest
{
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public Dictionary<string, JsonElement>? Specs { get; set; }
    /// <summary>Whether the variant sells as soon as its product does; omitted means not selling (owner opens it later).</summary>
    public bool? IsActive { get; set; }
}
