using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public class ProductImageServiceTests
{
    private readonly FakeProductRepository _products = new();
    private readonly FakeProductImageRepository _images = new();
    private readonly FakeUnitOfWork _work = new();
    private readonly FakeObjectStorage _storage = new();
    private readonly Product _product = ProductCommandTestData.NewProduct(Guid.NewGuid());
    private ProductImageService Service => new(_products, _images, _work, _storage);

    public ProductImageServiceTests() => _products.Products[_product.Id] = _product;
    private static byte[] Png(int size = 100)
    {
        var bytes = new byte[size];
        Convert.FromHexString("89504E470D0A1A0A").CopyTo(bytes, 0);
        return bytes;
    }
    private ProductImage AddImage(Guid? variant = null)
    {
        var image = new ProductImage { ProductId = _product.Id, VariantId = variant, SortOrder = _product.Images.Count,
            Url = "https://img.test/" + Guid.NewGuid(), ObjectKey = "products/" + Guid.NewGuid().ToString("N") + ".png", ContentType = "image/png", SizeBytes = 100 };
        _product.Images.Add(image);
        _storage.Objects[image.ObjectKey] = Png();
        _product.ImageUrl = ProductCoverImage.UrlOf(_product.Images);
        return image;
    }
    private Task<ProductImageCommandResult<TechStore.Shared.DTOs.ProductImageDto>> Upload(Guid? variant = null) => Service.UploadAsync(_product.Id, new("ignored.bin", Png(), variant));
    private static UpdateProductImagesLayoutRequest Layout(params ProductImage[] images) => new() { Items = images.Select(i => new ProductImageLayoutItemRequest { Id = i.Id, VariantId = i.VariantId }).ToList() };

    [Fact]
    public async Task UploadAsync_FirstSharedImage_StoresFileAddsRowAndSetsCover()
    {
        // Arrange
        var before = _product.UpdatedAt;
        // Act
        var result = await Upload();
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.Success);
        var image = _images.Added.Should().ContainSingle().Subject;
        _storage.Objects[image.ObjectKey].Should().Equal(Png());
        image.ContentType.Should().Be("image/png");
        image.ObjectKey.Should().StartWith($"products/{_product.Id:N}/").And.EndWith(".png");
        _product.ImageUrl.Should().Be(result.Data!.Url);
        _product.UpdatedAt.Should().Be(before);
        _work.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_AppendsAfterExistingImages()
    {
        // Arrange
        AddImage(); AddImage();
        // Act
        var result = await Upload();
        // Assert
        result.Data!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task UploadAsync_InvalidFile_ReturnsFileErrorAndStoresNothing()
    {
        // Arrange
        var upload = new ProductImageUpload("photo.jpg", "hello"u8.ToArray(), null);
        // Act
        var result = await Service.UploadAsync(_product.Id, upload);
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("file");
        _storage.PutCount.Should().Be(0);
        _images.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAsync_UnknownProduct_ReturnsNotFoundAndStoresNothing()
    {
        // Arrange
        _products.Products.Clear();
        // Act
        var result = await Upload();
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.NotFound);
        _storage.PutCount.Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_ForeignVariant_ReturnsVariantErrorAndStoresNothing()
    {
        // Arrange
        var variant = Guid.NewGuid();
        // Act
        var result = await Upload(variant);
        // Assert
        result.ValidationErrors.Should().ContainKey("variantId");
        _storage.PutCount.Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_TwentyImages_ReturnsConflictAndStoresNothing()
    {
        // Arrange
        for (var i = 0; i < 20; i++) AddImage();
        // Act
        var result = await Upload();
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.Conflict);
        _storage.PutCount.Should().Be(0);
        _product.Images.Should().HaveCount(20);
    }

    [Fact]
    public async Task UploadAsync_LimitReachedUnderLock_DeletesStoredFile()
    {
        // Arrange
        for (var i = 0; i < 19; i++) AddImage();
        _products.BeforeGetForUpdate = () => AddImage();
        // Act
        var result = await Upload();
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.Conflict);
        _storage.PutCount.Should().Be(1);
        _storage.DeletedKeys.Should().ContainSingle();
        _storage.Objects.Should().HaveCount(20);
        _work.RollbackCount.Should().Be(1);
        _images.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAsync_StorageDown_ReturnsStorageUnavailableAndNoRow()
    {
        // Arrange
        _storage.ThrowOnPut = true;
        // Act
        var result = await Upload();
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.StorageUnavailable);
        _images.Added.Should().BeEmpty();
        _work.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_SaveThrows_DeletesStoredFileAndRethrows()
    {
        // Arrange
        _work.ExceptionToThrowOnSave = new InvalidOperationException("db failed");
        // Act
        Func<Task> act = async () => await Upload();
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _storage.Objects.Should().BeEmpty();
        _storage.DeletedKeys.Should().ContainSingle();
        _work.RollbackCount.Should().Be(1);
        _work.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_VariantImage_KeepsSharedCover()
    {
        // Arrange
        var shared = AddImage();
        var variant = ProductCommandTestData.NewVariant(_product, "BLACK");
        // Act
        var result = await Upload(variant.Id);
        // Assert
        result.Data!.VariantId.Should().Be(variant.Id);
        _product.ImageUrl.Should().Be(shared.Url);
    }

    [Fact]
    public async Task DeleteAsync_MiddleImage_RenumbersAndDeletesFileAfterCommit()
    {
        // Arrange
        var first = AddImage(); var middle = AddImage(); var last = AddImage();
        // Act
        var result = await Service.DeleteAsync(_product.Id, middle.Id);
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.Success);
        _product.Images.Should().Equal(first, last);
        last.SortOrder.Should().Be(1);
        _work.CommitCount.Should().Be(1);
        _storage.DeletedKeys.Should().Equal(middle.ObjectKey);
        _storage.Objects.Should().NotContainKey(middle.ObjectKey);
    }

    [Fact]
    public async Task DeleteAsync_LastImage_ClearsCover()
    {
        // Arrange
        var image = AddImage();
        // Act
        await Service.DeleteAsync(_product.Id, image.Id);
        // Assert
        _product.ImageUrl.Should().BeNull();
        _product.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ImageOfAnotherProduct_ReturnsNotFound()
    {
        // Arrange
        AddImage();
        // Act
        var result = await Service.DeleteAsync(_product.Id, Guid.NewGuid());
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.NotFound);
        _product.Images.Should().ContainSingle();
        _storage.DeletedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownProduct_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        // Act
        var result = await Service.DeleteAsync(id, Guid.NewGuid());
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.NotFound);
    }

    [Fact]
    public async Task SaveLayoutAsync_Reorder_SetsSortOrderAndCover()
    {
        // Arrange
        var a = AddImage(); var b = AddImage(); var c = AddImage();
        // Act
        var result = await Service.SaveLayoutAsync(_product.Id, Layout(b, a, c));
        // Assert
        result.Data!.Select(i => i.Id).Should().Equal(b.Id, a.Id, c.Id);
        result.Data!.Select(i => i.SortOrder).Should().Equal(0, 1, 2);
        _product.ImageUrl.Should().Be(b.Url);
    }

    [Fact]
    public async Task SaveLayoutAsync_MissingImage_ReturnsItemsErrorAndChangesNothing()
    {
        // Arrange
        var a = AddImage(); var b = AddImage();
        // Act
        var result = await Service.SaveLayoutAsync(_product.Id, Layout(b));
        // Assert
        result.ValidationErrors.Should().ContainKey("items");
        _product.ImageUrl.Should().Be(a.Url);
        a.SortOrder.Should().Be(0); b.SortOrder.Should().Be(1);
        _work.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveLayoutAsync_AssignsVariant()
    {
        // Arrange
        var image = AddImage();
        var variant = ProductCommandTestData.NewVariant(_product, "BLACK");
        var request = Layout(image); request.Items![0].VariantId = variant.Id;
        // Act
        var result = await Service.SaveLayoutAsync(_product.Id, request);
        // Assert
        result.Data![0].VariantId.Should().Be(variant.Id);
        image.VariantId.Should().Be(variant.Id);
    }

    [Fact]
    public async Task SaveLayoutAsync_ForeignVariant_ReturnsIndexedError()
    {
        // Arrange
        var image = AddImage(); var request = Layout(image); request.Items![0].VariantId = Guid.NewGuid();
        // Act
        var result = await Service.SaveLayoutAsync(_product.Id, request);
        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].variantId");
        image.VariantId.Should().BeNull();
        _work.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveLayoutAsync_UnknownProduct_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateProductImagesLayoutRequest { Items = [] };
        // Act
        var result = await Service.SaveLayoutAsync(Guid.NewGuid(), request);
        // Assert
        result.Status.Should().Be(ProductImageCommandStatus.NotFound);
    }
}
