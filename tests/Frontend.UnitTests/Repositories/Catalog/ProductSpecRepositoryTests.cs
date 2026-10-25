using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Repositories.Orders;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public sealed class ProductSpecRepositoryTests
{
    private const string KeysJson = """
        [
          { "key": "ram_gb", "productCount": 3, "values": [
              { "value": 8, "label": "8", "filterValue": "8", "productCount": 2 },
              { "value": 16, "label": "16", "filterValue": "16", "productCount": 1 } ] },
          { "key": "bluetooth", "productCount": 2, "values": [
              { "value": "5.2", "label": "5.2", "filterValue": "\"5.2\"", "productCount": 2 } ] }
        ]
        """;

    [Fact]
    public async Task GetSpecKeysAsync_WithoutCategory_CallsTheBareRouteAndMapsKeysAndValues()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => KeysJson);
        IProductSpecRepository repository = new ProductRepository(api);

        // Act
        var keys = await repository.GetSpecKeysAsync(null);

        // Assert
        Assert.Equal("GET", api.Single.Method);
        Assert.Equal("api/products/spec-keys", api.Single.Path);
        Assert.Equal(["ram_gb", "bluetooth"], keys.Select(k => k.Key));
        Assert.Equal(3, keys[0].ProductCount);
        Assert.Equal(SpecValueKind.Number, keys[0].Values[1].Kind);
        Assert.Null(keys[0].Label);
        Assert.Equal("\"5.2\"", keys[1].Values[0].FilterValue);
    }

    [Fact]
    public async Task GetSpecKeysAsync_WithCategory_AddsTheCategoryId()
    {
        // Arrange
        var categoryId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var api = new RecordingApiClient((_, _, _) => "[]");
        IProductSpecRepository repository = new ProductRepository(api);

        // Act
        var keys = await repository.GetSpecKeysAsync(categoryId);

        // Assert
        Assert.Empty(keys);
        Assert.Equal("api/products/spec-keys?categoryId=11111111-2222-3333-4444-555555555555", api.Single.Path);
    }

    [Theory]
    [InlineData("""{ "key": "ram_gb" }""")]
    [InlineData("""[ { "key": "ram_gb", "productCount": 1 } ]""")]
    [InlineData("""[ { "key": "ram_gb", "productCount": 1, "values": [ { "label": "8", "productCount": 1 } ] } ]""")]
    public async Task GetSpecKeysAsync_MalformedResponse_ThrowsInvalidResponse(string json)
    {
        // Arrange
        IProductSpecRepository repository = new ProductRepository(new RecordingApiClient((_, _, _) => json));

        // Act
        var exception = await Assert.ThrowsAsync<ApiException>(() => repository.GetSpecKeysAsync(null));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task GetBrandsAsync_CallsTheBrandRouteAndMapsNamesAndCounts()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => """[ { "name": "Apple", "productCount": 3 }, { "name": "LG", "productCount": 1 } ]""");
        IProductBrandRepository repository = new ProductRepository(api);

        // Act
        var brands = await repository.GetBrandsAsync();

        // Assert
        Assert.Equal("api/products/brands", api.Single.Path);
        Assert.Equal([new BrandOption("Apple", 3), new BrandOption("LG", 1)], brands);
    }
}
