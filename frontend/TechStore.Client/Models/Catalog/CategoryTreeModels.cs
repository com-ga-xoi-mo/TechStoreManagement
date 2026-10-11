using System.Collections.Generic;

namespace TechStore.Client.Models.Catalog;

/// <summary>
/// Read-only category data for the inventory category-tree screen.
/// ProductCount is supplied by the category source, never inferred from a product page.
/// </summary>
public sealed record CategoryNode(
    string Id,
    string Name,
    string? ParentId,
    string? Description,
    int ProductCount,
    bool IsActive,
    IReadOnlyList<CategoryNode> Children);

/// <summary>
/// A complete hierarchy snapshot returned independently of product-list paging.
/// </summary>
public sealed record CategoryTreeSnapshot(
    IReadOnlyList<CategoryNode> Roots,
    bool IsPreviewData)
{
    public int CategoryCount => CountNodes(Roots);

    private static int CountNodes(IReadOnlyList<CategoryNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            count++;
            count += CountNodes(node.Children);
        }

        return count;
    }
}
