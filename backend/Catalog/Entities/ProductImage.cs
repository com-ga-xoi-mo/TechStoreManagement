using TechStore.Api.Common.Entities;

namespace TechStore.Api.Catalog.Entities;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public Product Product { get; set; } = null!;
    public ProductVariant? Variant { get; set; }
}
