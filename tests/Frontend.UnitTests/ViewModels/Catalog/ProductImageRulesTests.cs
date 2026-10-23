using TechStore.Client.Services.Files;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class ProductImageRulesTests
{
    [Theory]
    [InlineData("A.JPG", true)] [InlineData("b.webp", true)] [InlineData("c.heic", false)]
    public void IsAllowedExtension_FileName_ChecksAllowedType(string name, bool allowed)
    {
        // Arrange / Act
        var result = ProductImageRules.IsAllowedExtension(name);
        // Assert
        Assert.Equal(allowed, result);
    }
    [Fact]
    public void CoverIndex_SharedOrFallback_UsesFirstShared()
    {
        // Arrange / Act / Assert
        Assert.Equal(1, ProductImageRules.CoverIndex(new[] { false, true }, x => x));
        Assert.Equal(0, ProductImageRules.CoverIndex(new[] { false, false }, x => x));
        Assert.Equal(-1, ProductImageRules.CoverIndex(Array.Empty<bool>(), x => x));
    }
    [Fact]
    public void FilterPicked_WrongTypeAndOversize_NamesBothFiles()
    {
        // Arrange
        PickedImageFile[] files = [new("bad.heic", 10, [1]), new("big.jpg", ProductImageRules.MaxBytes + 1L, null)];
        // Act
        var result = ProductImageRules.FilterPicked(files, 0);
        // Assert
        Assert.Empty(result.Accepted);
        Assert.Equal(new[] { "bad.heic: chỉ nhận JPG, PNG, WebP", "big.jpg lớn hơn 5 MB" }, result.Messages);
    }
    [Theory]
    [InlineData(18, 2, "Chỉ thêm được 2 ảnh nữa (tối đa 20)")]
    [InlineData(20, 0, "Đã đủ 20 ảnh")]
    public void FilterPicked_OverLimit_AddsOnlyAvailableSlots(int existing, int accepted, string message)
    {
        // Arrange
        var files = Enumerable.Range(0, 4).Select(i => new PickedImageFile($"{i}.png", 1, [1])).ToArray();
        // Act
        var result = ProductImageRules.FilterPicked(files, existing);
        // Assert
        Assert.Equal(accepted, result.Accepted.Count);
        Assert.Equal(message, Assert.Single(result.Messages));
    }
}
