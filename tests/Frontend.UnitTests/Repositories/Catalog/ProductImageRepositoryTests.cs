using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public class ProductImageRepositoryTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid ImageId = Guid.NewGuid();
    private static string ImageJson(Guid? variant = null) => JsonSerializer.Serialize(new { id = ImageId, variantId = variant, url = "/uploads/a.png", contentType = "image/png", sizeBytes = 100, sortOrder = 0 });

    [Fact]
    public async Task UploadAsync_SharedImage_PostsFileWithoutVariantField()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => ImageJson());
        var repository = new ProductImageRepository(api);
        // Act
        var image = await repository.UploadAsync(ProductId, new("a.png", "image/png", [1, 2], null));
        // Assert
        Assert.Equal(ImageId, image.Id);
        Assert.Equal($"api/products/{ProductId:D}/images", api.Single.Path);
        var file = Assert.IsType<ApiFileContent>(api.Single.Body);
        Assert.Equal("file", file.FieldName); Assert.Equal("a.png", file.FileName);
        Assert.Equal(new byte[] { 1, 2 }, file.Content);
        Assert.Null(api.LastFields);
    }

    [Fact]
    public async Task UploadAsync_VariantImage_SendsVariantId()
    {
        // Arrange
        var variant = Guid.NewGuid(); var api = new RecordingApiClient((_, _, _) => ImageJson(variant));
        // Act
        var image = await new ProductImageRepository(api).UploadAsync(ProductId, new("a.png", "image/png", [1], variant));
        // Assert
        Assert.Equal(variant.ToString("D"), api.LastFields!["variantId"]);
        Assert.Equal(variant, image.VariantId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task UploadAsync_MalformedResponse_ThrowsInvalidResponse(string json)
    {
        // Arrange
        var repository = new ProductImageRepository(new RecordingApiClient((_, _, _) => json));
        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.UploadAsync(ProductId, new("a.png", "image/png", [1], null)));
        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task DeleteAsync_CallsDeleteRoute()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => "{}");
        // Act
        await new ProductImageRepository(api).DeleteAsync(ProductId, ImageId);
        // Assert
        Assert.Equal("DELETE", api.Single.Method);
        Assert.Equal($"api/products/{ProductId:D}/images/{ImageId:D}", api.Single.Path);
    }

    [Fact]
    public async Task SaveLayoutAsync_SendsItemsInOrderAndMapsList()
    {
        // Arrange
        var first = Guid.NewGuid();
        var api = new RecordingApiClient((_, _, _) => "[" + ImageJson() + "]");
        // Act
        var result = await new ProductImageRepository(api).SaveLayoutAsync(ProductId, [new(first, null), new(ImageId, null)]);
        // Assert
        Assert.Equal("PUT", api.Single.Method);
        Assert.Equal($"api/products/{ProductId:D}/images/layout", api.Single.Path);
        Assert.Equal(new Guid?[] { first, ImageId }, Assert.IsType<UpdateProductImagesLayoutRequest>(api.Single.Body).Items!.Select(i => i.Id));
        Assert.Equal(ImageId, Assert.Single(result).Id);
    }
}
