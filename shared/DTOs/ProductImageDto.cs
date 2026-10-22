namespace TechStore.Shared.DTOs;

public class ProductImageDto
{
    public Guid Id { get; set; }
    public Guid? VariantId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public int SortOrder { get; set; }
}
