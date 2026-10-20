using System.Text.Json;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public sealed class CategoryRepositoryTests
{
    [Fact]
    public async Task GetTreeAsync_FlattensNestedCategoriesWithoutCounts()
    {
        const string json = """
            [{"id":"11111111-1111-1111-1111-111111111111","name":"Thiết bị","slug":"thiet-bi","description":null,"parentId":null,"isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"subCategories":[{"id":"22222222-2222-2222-2222-222222222222","name":"Điện thoại","slug":"dien-thoai","description":null,"parentId":"11111111-1111-1111-1111-111111111111","isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"subCategories":[]}]}]
            """;
        var repository = new CategoryRepository(new JsonApiClient(json));

        var options = await repository.GetTreeAsync();

        Assert.Equal(2, options.Count);
        Assert.Equal("Thiết bị", options[0].DisplayPath);
        Assert.Equal("Thiết bị / Điện thoại", options[1].DisplayPath);
        Assert.Equal(options[0].Id, options[1].ParentId);
    }

    [Fact]
    public async Task GetTreeAsync_ReturnsAnEmptyTreeAsValidData()
    {
        var repository = new CategoryRepository(new JsonApiClient("[]"));

        var options = await repository.GetTreeAsync();

        Assert.Empty(options);
    }

    [Theory]
    [InlineData("{}")] 
    [InlineData("[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Root\"}]")]
    [InlineData("[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Root\",\"slug\":\"root\",\"parentId\":null,\"isActive\":true,\"subCategories\":[{\"id\":\"22222222-2222-2222-2222-222222222222\",\"name\":\"Child\",\"slug\":\"child\",\"parentId\":null,\"isActive\":true,\"subCategories\":[]}]}]")]
    public async Task GetTreeAsync_RejectsInvalidOrInconsistentTree(string json)
    {
        var repository = new CategoryRepository(new JsonApiClient(json));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetTreeAsync());

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetTreeAsync_PreservesHttpFailure()
    {
        var repository = new CategoryRepository(new ThrowingApiClient(
            ApiException.Http(500, null)));

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetTreeAsync());

        Assert.Equal(ApiErrorKind.Http, error.Kind);
        Assert.Equal(500, error.StatusCode);
    }

    private sealed class JsonApiClient(string json) : IApiClient
    {
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/categories/tree", relativePath);
            using var document = JsonDocument.Parse(json);
            return Task.FromResult((TResponse)(object)document.RootElement.Clone());
        }
        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingApiClient(ApiException error) : IApiClient
    {
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default) => throw error;
        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
