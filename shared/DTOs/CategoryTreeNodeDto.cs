namespace TechStore.Shared.DTOs;

public class CategoryTreeNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CategoryTreeNodeDto> SubCategories { get; set; } = [];
}
