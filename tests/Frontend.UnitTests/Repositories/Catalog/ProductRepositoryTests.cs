using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public sealed class ProductRepositoryTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task SearchAsync_MapsValidPageAndAcceptsLegitimateZeroFalseAndAdditiveFields()
    {
        const string json = """
            {"items":[{"id":"11111111-1111-1111-1111-111111111111","name":"Bàn phím","sku":"KB-01","brand":"Tech","categoryId":"22222222-2222-2222-2222-222222222222","categoryName":"Phụ kiện","priceFrom":0,"priceTo":0,"variantCount":0,"isSerialTracked":false,"isActive":false,"imageUrl":null,"newField":"ignored"}],"page":1,"pageSize":20,"totalCount":1,"totalPages":1,"addedEnvelopeField":true}
            """;
        var api = new StubApiClient(_ => json);
        var repository = new ProductRepository(api);

        var result = await repository.SearchAsync(new ProductQuery());

        var product = Assert.Single(result.Items);
        Assert.Equal(ProductId, product.Id);
        Assert.Equal("Bàn phím", product.Name);
        Assert.Equal(0m, product.PriceFrom);
        Assert.Equal(0, product.VariantCount);
        Assert.False(product.IsSerialTracked);
        Assert.False(product.IsActive);
        Assert.Null(product.ImageUrl);
        Assert.Equal(1, result.TotalCount);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"items\":null,\"page\":1,\"pageSize\":20,\"totalCount\":0,\"totalPages\":0}")]
    [InlineData("{\"items\":[],\"page\":1,\"pageSize\":20,\"totalPages\":0}")]
    public async Task SearchAsync_RejectsInvalidOrMissingEnvelopeFields(string json)
    {
        var repository = new ProductRepository(new StubApiClient(_ => json));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new ProductQuery()));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SearchAsync_IgnoresUnknownEnvelopeFields()
    {
        const string json = "{\"items\":[],\"page\":1,\"pageSize\":20,\"totalCount\":0,\"totalPages\":0,\"futureField\":true}";
        var repository = new ProductRepository(new StubApiClient(_ => json));

        var result = await repository.SearchAsync(new ProductQuery());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_RejectsMissingItemFieldEvenWhenDtoWouldDefaultIt()
    {
        const string json = """
            {"items":[{"id":"11111111-1111-1111-1111-111111111111","name":"Keyboard","sku":"KB-01","brand":"Tech","categoryId":"22222222-2222-2222-2222-222222222222","categoryName":"Phụ kiện","priceFrom":0,"priceTo":0,"variantCount":0,"isSerialTracked":false,"imageUrl":null}],"page":1,"pageSize":20,"totalCount":1,"totalPages":1}
            """;
        var repository = new ProductRepository(new StubApiClient(_ => json));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new ProductQuery()));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SearchAsync_RejectsInconsistentPageMetadata()
    {
        const string json = """
            {"items":[],"page":9,"pageSize":20,"totalCount":1,"totalPages":0}
            """;
        var repository = new ProductRepository(new StubApiClient(_ => json));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new ProductQuery()));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetByIdAsync_MapsDetailAndClonesTypedSpecs()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","categoryId":"22222222-2222-2222-2222-222222222222","categoryName":"Điện thoại","name":"Phone","sku":"P-1","barcode":null,"brand":"Tech","basePrice":10.25,"costPrice":5,"description":null,"imageUrl":null,"specs":{"ram_gb":16,"wireless":true,"chip":"M3"},"isSerialTracked":true,"isActive":false,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"variants":[{"id":"33333333-3333-3333-3333-333333333333","productId":"11111111-1111-1111-1111-111111111111","variantName":"Silver","sku":"P-1-S","barcode":null,"price":10.25,"specs":{"color":"Silver"},"effectiveSpecs":{"ram_gb":16,"wireless":true,"chip":"M3","color":"Silver"},"isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null}]}
            """;
        using var document = JsonDocument.Parse(json);
        var repository = new ProductRepository(new StubApiClient(_ => document.RootElement));

        var product = await repository.GetByIdAsync(ProductId);

        Assert.NotNull(product);
        Assert.Equal(10.25m, product.BasePrice);
        Assert.Equal(JsonValueKind.Number, product.Specs["ram_gb"].ValueKind);
        Assert.Equal(16, product.Specs["ram_gb"].GetInt32());
        Assert.Equal(JsonValueKind.True, product.Specs["wireless"].ValueKind);
        var variant = Assert.Single(product.Variants);
        Assert.Equal("Silver", variant.Specs["color"].GetString());
        Assert.Equal(16, variant.EffectiveSpecs["ram_gb"].GetInt32());
    }

    [Theory]
    [InlineData("{\"id\":\"11111111-1111-1111-1111-111111111111\"}")]
    [InlineData("null")]
    public async Task GetByIdAsync_RejectsInvalidOrMissingFields(string json)
    {
        var repository = new ProductRepository(new StubApiClient(_ => json));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(ProductId));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullOnlyForHttpNotFound()
    {
        var repository = new ProductRepository(new StubApiClient(_ =>
            throw ApiException.Http((int)HttpStatusCode.NotFound, null)));

        Assert.Null(await repository.GetByIdAsync(ProductId));

        var serverErrorRepository = new ProductRepository(new StubApiClient(_ =>
            throw ApiException.Http((int)HttpStatusCode.InternalServerError, null)));
        var error = await Assert.ThrowsAsync<ApiException>(() => serverErrorRepository.GetByIdAsync(ProductId));
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_RejectsResponseForDifferentProductId()
    {
        const string json = """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","categoryId":"22222222-2222-2222-2222-222222222222","categoryName":"A","name":"A","sku":"A","barcode":null,"brand":"A","basePrice":0,"costPrice":0,"description":null,"imageUrl":null,"specs":{},"isSerialTracked":true,"isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"variants":[]}
            """;
        var repository = new ProductRepository(new StubApiClient(_ => json));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(ProductId));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    private sealed class StubApiClient(Func<string, object> response) : IApiClient
    {
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = response(relativePath);
            if (value is string json)
            {
                using var document = JsonDocument.Parse(json);
                return Task.FromResult((TResponse)(object)document.RootElement.Clone());
            }
            return Task.FromResult((TResponse)value);
        }

        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
