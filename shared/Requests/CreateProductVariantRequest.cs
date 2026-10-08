using System.Text.Json;

namespace TechStore.Shared.Requests;

public class CreateProductVariantRequest
{
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public Dictionary<string, JsonElement>? Specs { get; set; }
    public bool? IsActive { get; set; }
}
