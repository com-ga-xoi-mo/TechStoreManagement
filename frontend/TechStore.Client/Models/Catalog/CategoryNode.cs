namespace TechStore.Client.Models.Catalog;

/// <summary>API-backed category data; null statistics mean unavailable, not zero.</summary>
public sealed record CategoryNode(
    Guid Id, string Name, string Slug, bool IsActive,
    IReadOnlyList<CategoryNode> Children,
    int? ProductCount = null, int? SkuCount = null,
    string? Description = null, Guid? ParentId = null);
