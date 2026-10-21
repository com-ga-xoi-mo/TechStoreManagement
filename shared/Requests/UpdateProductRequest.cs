using System.Text.Json;

namespace TechStore.Shared.Requests;

public class UpdateProductRequest
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? CostPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, JsonElement>? Specs { get; set; }
    /// <summary>Names for new spec keys used in this request (design D13); existing definitions are kept.</summary>
    public Dictionary<string, SpecLabelRequest>? SpecLabels { get; set; }
    public bool? IsSerialTracked { get; set; }
    public List<UpdateProductVariantRequest>? Variants { get; set; }
}
