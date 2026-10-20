using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public sealed class ProductCommandRepositoryTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static string DetailJson(Guid id, bool isActive = true) => $$"""
        {
            "id": "{{id}}",
            "categoryId": "{{CategoryId}}",
            "categoryName": "Điện thoại",
            "name": "Phone",
            "sku": "P-1",
            "barcode": null,
            "brand": "Tech",
            "basePrice": 10.25,
            "costPrice": 5,
            "description": null,
            "imageUrl": null,
            "specs": {"ram_gb": 16},
            "isSerialTracked": true,
            "isActive": {{(isActive ? "true" : "false")}},
            "createdAt": "2026-01-01T00:00:00Z",
            "updatedAt": null,
            "variants": [
                {
                    "id": "33333333-3333-3333-3333-333333333333",
                    "productId": "{{id}}",
                    "variantName": "Silver",
                    "sku": "P-1-S",
                    "barcode": null,
                    "price": 10.25,
                    "specs": {"color": "Silver"},
                    "effectiveSpecs": {"ram_gb": 16, "color": "Silver"},
                    "isActive": true,
                    "createdAt": "2026-01-01T00:00:00Z",
                    "updatedAt": null
                }
            ]
        }
        """;

    [Fact]
    public async Task CreateAsync_PostsToApiProductsWithRequestAndMapsResponse()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            CategoryId = CategoryId,
            Name = "Phone",
            Sku = "P-1",
            Brand = "Tech",
            BasePrice = 10.25m,
            CostPrice = 5m
        };

        var api = new RecordingApiClient((method, path, body) =>
        {
            Assert.Equal("POST", method);
            Assert.Equal("api/products", path);
            Assert.Same(request, body);
            return DetailJson(ProductId);
        });

        IProductCommandRepository repository = new ProductRepository(api);

        // Act
        var result = await repository.CreateAsync(request);

        // Assert
        Assert.Equal(ProductId, result.Id);
        Assert.Equal("Phone", result.Name);
        var call = api.Single;
        Assert.Equal("POST", call.Method);
        Assert.Equal("api/products", call.Path);
    }

    [Fact]
    public async Task UpdateAsync_UsesPutAndMapsResponse()
    {
        // Arrange
        var request = new UpdateProductRequest
        {
            CategoryId = CategoryId,
            Name = "Phone Updated",
            Brand = "Tech",
            BasePrice = 12m,
            CostPrice = 6m
        };

        var api = new RecordingApiClient((method, path, body) =>
        {
            Assert.Equal("PUT", method);
            Assert.Equal($"api/products/{ProductId:D}", path);
            Assert.Same(request, body);
            return DetailJson(ProductId);
        });

        IProductCommandRepository repository = new ProductRepository(api);

        // Act
        var result = await repository.UpdateAsync(ProductId, request);

        // Assert
        Assert.Equal(ProductId, result.Id);
        var call = api.Single;
        Assert.Equal("PUT", call.Method);
        Assert.Equal($"api/products/{ProductId:D}", call.Path);
    }

    [Fact]
    public async Task UpdateAsync_WhenResponseIdDiffersFromRequestedId_ThrowsInvalidResponse()
    {
        // Arrange
        var differentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var request = new UpdateProductRequest { Name = "Phone" };

        var api = new RecordingApiClient((_, _, _) => DetailJson(differentId));
        IProductCommandRepository repository = new ProductRepository(api);

        // Act & Assert
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.UpdateAsync(ProductId, request));
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SetStatusAsync_UsesPatchWithUpdateProductStatusRequestAndMapsResponse()
    {
        // Arrange
        var api = new RecordingApiClient((method, path, body) =>
        {
            Assert.Equal("PATCH", method);
            Assert.Equal($"api/products/{ProductId:D}/status", path);
            var statusRequest = Assert.IsType<UpdateProductStatusRequest>(body);
            Assert.False(statusRequest.IsActive);
            return DetailJson(ProductId, isActive: false);
        });

        IProductCommandRepository repository = new ProductRepository(api);

        // Act
        var result = await repository.SetStatusAsync(ProductId, false);

        // Assert
        Assert.Equal(ProductId, result.Id);
        Assert.False(result.IsActive);
        var call = api.Single;
        Assert.Equal("PATCH", call.Method);
        Assert.Equal($"api/products/{ProductId:D}/status", call.Path);
    }

    [Fact]
    public async Task DeleteAsync_RecordsDeleteWithoutBody()
    {
        // Arrange
        var api = new RecordingApiClient((method, path, body) =>
        {
            Assert.Equal("DELETE", method);
            Assert.Equal($"api/products/{ProductId:D}", path);
            Assert.Null(body);
            return "{}";
        });

        IProductCommandRepository repository = new ProductRepository(api);

        // Act
        await repository.DeleteAsync(ProductId);

        // Assert
        var call = api.Single;
        Assert.Equal("DELETE", call.Method);
        Assert.Equal($"api/products/{ProductId:D}", call.Path);
        Assert.Null(call.Body);
    }

    [Fact]
    public async Task DeleteAsyncAndUpdateAsync_WhenApiThrows409Conflict_ReachesCallerUnchanged()
    {
        // Arrange
        var conflict = RecordingApiClient.Http(409, "in use");
        var api = new RecordingApiClient((_, _, _) => throw conflict);
        IProductCommandRepository repository = new ProductRepository(api);

        // Act & Assert for DeleteAsync
        var deleteError = await Assert.ThrowsAsync<ApiException>(() => repository.DeleteAsync(ProductId));
        Assert.Same(conflict, deleteError);
        Assert.Equal(409, deleteError.StatusCode);
        Assert.Equal("in use", deleteError.Detail);

        // Act & Assert for UpdateAsync
        var updateError = await Assert.ThrowsAsync<ApiException>(() => repository.UpdateAsync(ProductId, new UpdateProductRequest()));
        Assert.Same(conflict, updateError);
        Assert.Equal(409, updateError.StatusCode);
        Assert.Equal("in use", updateError.Detail);
    }

    [Fact]
    public async Task GuidEmpty_ThrowsArgumentExceptionWithoutAnyCall()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => throw new InvalidOperationException("Should not be called"));
        IProductCommandRepository repository = new ProductRepository(api);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync(Guid.Empty, new UpdateProductRequest()));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SetStatusAsync(Guid.Empty, true));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.DeleteAsync(Guid.Empty));
        Assert.Empty(api.Calls);
    }
}
