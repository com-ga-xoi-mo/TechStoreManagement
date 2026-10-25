namespace TechStore.Client.Features.Categories.Models;

/// <summary>API-backed category data; null statistics mean unavailable, not zero.</summary>
public sealed record CategoryNode(
    Guid Id, string Name, string Slug, bool IsActive,
    IReadOnlyList<CategoryNode> Children,
    int? ProductCount = null, int? SkuCount = null,
    string? Description = null, Guid? ParentId = null);

public enum CategoryStatusFilter { All, Active, Hidden }

public enum CategoryWorkspaceTab { Categories, Inventory, SerialImei }

public sealed record CategoryStatusOption(CategoryStatusFilter Value, string Label);

public sealed record CategoryIconDescriptor(string Symbol, string Background, string Foreground, string Border);
