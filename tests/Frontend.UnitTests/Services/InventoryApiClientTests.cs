using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Services;

public sealed class InventoryApiClientTests
{
    [Fact]
    public async Task GetStocksAsync_ForwardsQueryAndPreservesPageMetadata()
    {
        var expected = new PagedResultDto<InventoryStockDto>
        {
            Items = [new InventoryStockDto { ProductName = "Phone", ProductSku = "P-1", Quantity = 10, ReservedQuantity = 2 }],
            Page = 2, PageSize = 20, TotalCount = 21, TotalPages = 2
        };
        var api = new RecordingApiClient(expected);
        var client = new InventoryApiClient(api);

        var result = await client.GetStocksAsync(new InventoryStockFilterRequest
        {
            Page = 2, PageSize = 20, LowStock = false,
            ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            VariantId = Guid.Parse("22222222-2222-2222-2222-222222222222")
        });

        Assert.Equal(expected.TotalCount, result.TotalCount);
        Assert.Equal(expected.TotalPages, result.TotalPages);
        Assert.Equal("api/inventory/stocks?productId=11111111-1111-1111-1111-111111111111&variantId=22222222-2222-2222-2222-222222222222&lowStock=false&page=2&pageSize=20", api.Path);
    }

    [Fact]
    public async Task GetStocksAsync_AllowsEmptyPageWithoutFabricatingRows()
    {
        var api = new RecordingApiClient(new PagedResultDto<InventoryStockDto>
        {
            Items = [], Page = 1, PageSize = 20, TotalCount = 0, TotalPages = 0
        });

        var result = await new InventoryApiClient(api).GetStocksAsync(new() { Page = 1, PageSize = 20 });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetStocksAsync_PropagatesNormalizedApiErrors()
    {
        var expected = ApiException.Http(403, null);
        var api = new RecordingApiClient(expected);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            new InventoryApiClient(api).GetStocksAsync(new() { Page = 1, PageSize = 20 }));

        Assert.Same(expected, error);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task GetStocksAsync_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var api = new RecordingApiClient(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new InventoryApiClient(api).GetStocksAsync(new() { Page = 1, PageSize = 20 }, cancellation.Token));
    }

    [Fact]
    public async Task GetStocksAsync_RejectsInvalidPageSize()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new InventoryApiClient(new RecordingApiClient(new PagedResultDto<InventoryStockDto>()))
                .GetStocksAsync(new() { Page = 1, PageSize = 101 }));
    }

    [Fact]
    public async Task DeleteStockAsync_ForwardsAuthorizedDeleteRoute()
    {
        var api = new RecordingApiClient(new PagedResultDto<InventoryStockDto>());
        var stockId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var result = await new InventoryApiClient(api).DeleteStockAsync(stockId);

        Assert.True(result.Succeeded);
        Assert.Equal($"api/inventory/stocks/{stockId:D}", api.Path);
    }

    [Fact]
    public async Task DeleteStockAsync_MapsConflictToRecoverableTypedFailure()
    {
        var api = new RecordingApiClient(ApiException.Http(409, new ApiProblemDetails(
            null,
            "Inventory stock cannot be deleted",
            "Stock has movement history.",
            409,
            null,
            null,
            new Dictionary<string, IReadOnlyList<string>>())));

        var result = await new InventoryApiClient(api).DeleteStockAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(InventoryStockDeleteFailure.Conflict, result.Failure);
        Assert.Equal("Stock has movement history.", result.Message);
    }

    private sealed class RecordingApiClient : IApiClient
    {
        private readonly object _result;
        public string? Path { get; private set; }

        public RecordingApiClient(object result) => _result = result;

        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            Path = relativePath;
            cancellationToken.ThrowIfCancellationRequested();
            if (_result is Exception exception) return Task.FromException<TResponse>(exception);
            return Task.FromResult((TResponse)_result);
        }

        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            Path = relativePath;
            cancellationToken.ThrowIfCancellationRequested();
            if (_result is Exception exception) return Task.FromException(exception);
            return Task.CompletedTask;
        }
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
