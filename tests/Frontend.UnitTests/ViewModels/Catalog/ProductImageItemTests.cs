using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class ProductImageItemTests
{
    [Fact]
    public void MarkSaved_ClearsPendingAndIsNew()
    {
        // Arrange
        var item = ProductImageItem.FromPicked(new("a.png", 1, [1])); var id = Guid.NewGuid();
        // Act
        item.MarkSaved(id, "/uploads/a.png");
        // Assert
        Assert.Equal(id, item.Id); Assert.False(item.IsNew); Assert.Null(item.PendingContent); Assert.Equal("/uploads/a.png", item.Preview.Url);
    }
    [Fact]
    public void VariantLabel_FollowsRowRename()
    {
        // Arrange
        var row = new VariantRowItem(null) { VariantName = "Đen" };
        var item = ProductImageItem.FromPicked(new("a.png", 1, [1])); item.Variant = row;
        var changed = new List<string?>(); item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        // Act
        row.VariantName = "Đen mới";
        // Assert
        Assert.Equal("Đen mới", item.VariantLabel); Assert.Contains(nameof(item.VariantLabel), changed);
    }
}
