namespace TechStore.Shared.DTOs;

public class CategoryTreeNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    // Nullable for backward-compatible clients when an older API omits statistics;
    // the category tree endpoint now populates both values.
    public int? ProductCount { get; set; }
    public int? SkuCount { get; set; }
    public List<CategoryTreeNodeDto> SubCategories { get; set; } = [];
}
