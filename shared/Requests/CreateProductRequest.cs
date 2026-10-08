using System.Text.Json;

namespace TechStore.Shared.Requests;

public class CreateProductRequest
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? Brand { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? CostPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, JsonElement>? Specs { get; set; }
    public bool? IsSerialTracked { get; set; }
    public List<CreateProductVariantRequest>? Variants { get; set; }
}
