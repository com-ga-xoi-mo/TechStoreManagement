using TechStore.Client.Models.Catalog;
using Xunit;

namespace TechStore.Client.Tests;

public sealed class CategoryTreeModelTests
{
    [Fact]
    public void Snapshot_CategoryCount_IncludesAllDescendants()
    {
        var snapshot = new CategoryTreeSnapshot(
        [
            new CategoryNode("phones", "Điện thoại", null, null, 8, true,
            [
                new CategoryNode("ios", "iOS", "phones", "Apple", 3, true, []),
                new CategoryNode("android", "Android", "phones", null, 5, false, [])
            ])
        ],
        IsPreviewData: true);

        Assert.Equal(3, snapshot.CategoryCount);
        Assert.True(snapshot.IsPreviewData);
        Assert.Equal("phones", snapshot.Roots[0].Children[0].ParentId);
    }

    [Fact]
    public void CategoryNode_KeepsSourceCountAndChildrenIndependent()
    {
        var child = new CategoryNode("child", "Child", "root", null, 4, true, []);
        var root = new CategoryNode("root", "Root", null, "Root description", 9, true, [child]);

        Assert.Equal(9, root.ProductCount);
        Assert.Equal(4, root.Children[0].ProductCount);
        Assert.Equal("Root description", root.Description);
    }
}
