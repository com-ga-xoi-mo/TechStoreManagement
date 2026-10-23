using TechStore.Client.Models.Catalog;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class ProductImageViewerTests
{
    private static ProductImage Image(int order, Guid? variant = null) => new(Guid.NewGuid(), variant, $"/{order}.png", "image/png", 1, order);
    [Fact]
    public void FilterByVariant_ShowsSharedAndVariantAndSelectsFirstVariant()
    {
        // Arrange
        var product = CatalogSamples.Detail(Guid.NewGuid()); var black = product.Variants[0]; var white = CatalogSamples.Variant(product.Id, "WHITE");
        var shared = Image(0); var a = Image(1, black.Id); var b = Image(2, black.Id); var c = Image(3, white.Id);
        product = product with { Variants = [black, white], Images = [shared, a, b, c] };
        var viewer = new ProductImageViewer(); viewer.Load(product);
        // Act
        viewer.SelectedFilter = viewer.Filters[1];
        // Assert
        Assert.Equal(new Guid?[] { shared.Id, a.Id, b.Id }, viewer.VisibleImages.Select(i => i.Id)); Assert.Equal(a.Url, viewer.SelectedUrl); Assert.True(viewer.HasThumbnails);
        viewer.SelectImageCommand.Execute(viewer.VisibleImages[0]); Assert.Equal(shared.Url, viewer.SelectedUrl);
    }
    [Fact]
    public void SingleImage_HidesThumbnails()
    {
        // Arrange
        var viewer = new ProductImageViewer(); var image = Image(0);
        // Act
        viewer.Load(CatalogSamples.Detail(Guid.NewGuid()) with { Images = [image] });
        // Assert
        Assert.False(viewer.HasThumbnails); Assert.Equal(image.Url, viewer.SelectedUrl);
    }
    [Fact]
    public void Load_NoImages_SelectedUrlNull()
    {
        // Arrange
        var viewer = new ProductImageViewer();
        // Act
        viewer.Load(CatalogSamples.Detail(Guid.NewGuid()));
        // Assert
        Assert.Null(viewer.SelectedUrl); Assert.False(viewer.HasThumbnails);
    }
    [Fact]
    public void Filters_OnlyVariantsWithImages()
    {
        // Arrange
        var product = CatalogSamples.Detail(Guid.NewGuid()); var other = CatalogSamples.Variant(product.Id, "OTHER");
        var viewer = new ProductImageViewer();
        // Act
        viewer.Load(product with { Variants = [other, product.Variants[0]], Images = [Image(0, product.Variants[0].Id)] });
        // Assert
        Assert.Equal(2, viewer.Filters.Count); Assert.Equal("Tất cả", viewer.Filters[0].Label); Assert.Equal(product.Variants[0].Id, viewer.Filters[1].VariantId);
    }
}
