using System.Net;
using System.Text;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public sealed class ProductRepositoryQueryTests
{
    [Fact]
    public async Task SearchAsync_EncodesEachQueryValueAndPreservesContractOrder()
    {
        const string responseJson = "{\"items\":[],\"page\":2,\"pageSize\":50,\"totalCount\":0,\"totalPages\":0}";
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(Json(responseJson)));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var repository = new ProductRepository(new ApiClient(client,
            new ApiClientOptions { BaseUrl = "https://localhost:7207/" }));
        var categoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await repository.SearchAsync(new ProductQuery
        {
            Query = " Máy & ABC+%# ",
            Brand = "ACME, Inc.",
            CategoryId = categoryId,
            IsActive = false,
            MinPrice = 10.20m,
            MaxPrice = 99m,
            Specs = ["cpu:Apple M3 & A:B"],
            Sort = "price:asc",
            Page = 2,
            PageSize = 50
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://localhost:7207/api/products", request.RequestUri?.GetLeftPart(UriPartial.Path));
        Assert.Equal(
            "q=M%C3%A1y%20%26%20ABC%2B%25%23&brand=ACME%2C%20Inc.&categoryId=22222222-2222-2222-2222-222222222222&isActive=false&minPrice=10.20&maxPrice=99&spec=cpu%3AApple%20M3%20%26%20A%3AB&sort=price%3Aasc&page=2&pageSize=50",
            request.RequestUri?.GetComponents(UriComponents.Query, UriFormat.UriEscaped));
        Assert.Null(request.Authorization);
    }

    [Theory]
    [InlineData(0, 20, "createdAt:desc", null, null, null)]
    [InlineData(1, 101, "createdAt:desc", null, null, null)]
    [InlineData(1, 20, "stock:desc", null, null, null)]
    [InlineData(1, 20, "price:sideways", null, null, null)]
    [InlineData(1, 20, "createdAt:desc", "-1", null, null)]
    [InlineData(1, 20, "createdAt:desc", "20", "10", null)]
    [InlineData(1, 20, "createdAt:desc", null, null, "RAM:16")]
    [InlineData(1, 20, "createdAt:desc", null, null, "ram_gb:")]
    [InlineData(1, 20, "createdAt:desc", null, null, "ram_gb:16|ram_gb:32")]
    public async Task SearchAsync_RejectsInvalidCriteriaBeforeSending(
        int page,
        int pageSize,
        string sort,
        string? min,
        string? max,
        string? spec)
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(Json("{}")));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var repository = new ProductRepository(new ApiClient(client,
            new ApiClientOptions { BaseUrl = "https://localhost:7207/" }));
        var query = new ProductQuery
        {
            Page = page,
            PageSize = pageSize,
            Sort = sort,
            MinPrice = min is null ? null : decimal.Parse(min, System.Globalization.CultureInfo.InvariantCulture),
            MaxPrice = max is null ? null : decimal.Parse(max, System.Globalization.CultureInfo.InvariantCulture)
        };
        if (spec is not null) query = query with { Specs = spec.Split('|') };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => repository.SearchAsync(query));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SearchAsync_PreservesHttpStatusAndDoesNotRetry(HttpStatusCode status)
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            Json("{\"title\":\"Request failed\",\"status\":400,\"errors\":{\"q\":[\"Invalid query\"]}}", status)));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var repository = new ProductRepository(new ApiClient(client,
            new ApiClientOptions { BaseUrl = "https://localhost:7207/" }));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new ProductQuery()));

        Assert.Equal(ApiErrorKind.Http, error.Kind);
        Assert.Equal((int)status, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_PropagatesCallerCancellationToTransport()
    {
        var handler = new RecordingHttpMessageHandler((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Json("{\"items\":[],\"page\":1,\"pageSize\":20,\"totalCount\":0,\"totalPages\":0}"));
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var repository = new ProductRepository(new ApiClient(client,
            new ApiClientOptions { BaseUrl = "https://localhost:7207/" }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.SearchAsync(new ProductQuery(), cancellation.Token));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
