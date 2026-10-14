using System.Net;
using System.Text;
using System.Text.Json;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Infrastructure;
using TechStore.Shared.DTOs;

namespace TechStore.Client.UnitTests.Transport;

public sealed class ApiClientTransportTests
{
    [Fact]
    public async Task GetAsync_PreservesRelativeRouteAndEncodedQuery()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("{}")));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        await api.GetAsync<Dictionary<string, string>>("products?search=a%26b");

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://localhost:7207/api/products?search=a%26b", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task PostAsync_SerializesRequestAndDeserializesResponse()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("{\"id\":7,\"extra\":true}", HttpStatusCode.Created)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var result = await api.PostAsync<CreateRequest, CreateResponse>("products", new("Keyboard"));

        // Assert
        Assert.Equal(7, result.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("application/json; charset=utf-8", request.ContentHeaders["Content-Type"].Single());
        Assert.Equal("{\"name\":\"Keyboard\"}", request.Body);
    }

    [Theory]
    [InlineData("https://attacker.test/steal")]
    [InlineData("//attacker.test/steal")]
    [InlineData("\\\\attacker.test\\steal")]
    public async Task GetAsync_RejectsForeignOrAbsoluteRoutesBeforeSending(string path)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("{}")));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var error = await Assert.ThrowsAsync<ArgumentException>(() => api.GetAsync<JsonElement>(path));

        // Assert
        Assert.Contains("relative", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PostAsync_DoesNotRetryFailedWrite()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("{\"title\":\"Conflict\"}", HttpStatusCode.Conflict)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(
            () => api.PostAsync<CreateRequest>("products", new("Keyboard")));

        // Assert
        Assert.Equal((int)HttpStatusCode.Conflict, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsync_DoesNotFollowRedirectFromPrimaryHandler()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("https://attacker.test/redirect") }
            }));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<JsonElement>("products"));

        // Assert
        Assert.Equal((int)HttpStatusCode.Redirect, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsync_DeserializesPagedContractAndPreservesDynamicJsonValues()
    {
        // Arrange
        const string json = """{"items":[{"name":"Laptop","specs":{"ram":16,"available":true,"note":null}}],"page":2,"pageSize":5,"totalCount":6,"totalPages":2,"added":"ignored"}""";
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(json)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var result = await api.GetAsync<PagedResultDto<JsonElement>>("products?page=2");

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal("Laptop", result.Items[0].GetProperty("name").GetString());
        var specs = result.Items[0].GetProperty("specs");
        Assert.Equal(JsonValueKind.Number, specs.GetProperty("ram").ValueKind);
        Assert.Equal(JsonValueKind.True, specs.GetProperty("available").ValueKind);
        Assert.Equal(JsonValueKind.Null, specs.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task GetAsync_DeserializesSharedProductSummaryPagedContract()
    {
        // Arrange
        const string json = """{"items":[{"id":"8a5d764a-43b3-4aae-9996-f2dbe1252990","name":"Laptop","sku":"LAP-1","brand":"TechStore","categoryId":"4dca9e87-b8e0-4a58-bc72-12bd2647aabc","categoryName":"Laptops","priceFrom":1000,"priceTo":1500,"variantCount":2,"isSerialTracked":true,"isActive":true,"extra":"ignored"}],"page":1,"pageSize":10,"totalCount":1,"totalPages":1}""";
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(json)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var result = await api.GetAsync<PagedResultDto<ProductSummaryDto>>("api/products");

        // Assert
        var product = Assert.Single(result.Items);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal(1000m, product.PriceFrom);
        Assert.Equal(2, product.VariantCount);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsValidEmptyPagedList()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse("{\"items\":[],\"page\":1,\"pageSize\":10,\"totalCount\":0,\"totalPages\":0}")));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var result = await api.GetAsync<PagedResultDto<CreateResponse>>("products");

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("\"wrong-shape\"")]
    public async Task GetAsync_RejectsNullEmptyMalformedOrWrongJson(string json)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(json)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<CreateResponse>("products/1"));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
        Assert.Equal((int)HttpStatusCode.OK, error.StatusCode);
        if (json.Length > 0)
        {
            Assert.DoesNotContain(json, error.Message);
        }
    }

    [Fact]
    public async Task PostAsync_WithoutResponseType_AcceptsNoContent()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act / Assert
        await api.PostAsync("products/1/refresh", new CreateRequest("Keyboard"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PostAsync_WithResponseType_RejectsNoContent()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(
            () => api.PostAsync<CreateRequest, CreateResponse>("products", new("Keyboard")));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
        Assert.Equal((int)HttpStatusCode.NoContent, error.StatusCode);
    }

    [Fact]
    public async Task GetAsync_DisposesResponseContentAfterReading()
    {
        // Arrange
        var content = new TrackingStringContent("{\"id\":7}");
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        _ = await api.GetAsync<CreateResponse>("products/1");

        // Assert
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task PostAsync_DisposesErrorResponseContent()
    {
        // Arrange
        var content = new TrackingStringContent("{\"title\":\"Conflict\"}");
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Conflict) { Content = content }));
        using var client = CreateClient(handler);
        var api = new ApiClient(client, Options());

        // Act
        _ = await Assert.ThrowsAsync<ApiException>(
            () => api.PostAsync<CreateRequest, CreateResponse>("products", new("Keyboard")));

        // Assert
        Assert.True(content.Disposed);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static ApiClientOptions Options() => new() { BaseUrl = "https://localhost:7207/api" };

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class TrackingStringContent(string content) : StringContent(content, Encoding.UTF8, "application/json")
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed record CreateRequest(string Name);

    private sealed record CreateResponse(int Id);
}
