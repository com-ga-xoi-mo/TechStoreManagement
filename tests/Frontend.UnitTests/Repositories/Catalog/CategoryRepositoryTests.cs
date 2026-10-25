using System.Text.Json;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Catalog;

public sealed class CategoryRepositoryTests
{
    [Fact]
    public async Task GetTreeAsync_FlattensNestedCategoriesWithoutCounts()
    {
        const string json = """
            [{"id":"11111111-1111-1111-1111-111111111111","name":"Thiết bị","slug":"thiet-bi","description":null,"parentId":null,"isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"subCategories":[{"id":"22222222-2222-2222-2222-222222222222","name":"Điện thoại","slug":"dien-thoai","description":null,"parentId":"11111111-1111-1111-1111-111111111111","isActive":false,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"subCategories":[]}]}]
            """;
        var repository = new CategoryRepository(new JsonApiClient(json));

        var options = await repository.GetTreeAsync();

        Assert.Equal(2, options.Count);
        Assert.Equal("Thiết bị", options[0].DisplayPath);
        Assert.True(options[0].IsActive);
        Assert.Equal("Thiết bị", options[0].Label);
        Assert.Equal("Thiết bị / Điện thoại", options[1].DisplayPath);
        Assert.False(options[1].IsActive);
        Assert.Equal("Thiết bị / Điện thoại (đang ẩn)", options[1].Label);
        Assert.Equal(options[0].Id, options[1].ParentId);
    }

    [Fact]
    public void CategoryOption_Label_AddsHiddenSuffix()
    {
        // Arrange
        var active = new TechStore.Client.Models.Catalog.CategoryOption(Guid.NewGuid(), null, "Laptop", "Thiết bị / Laptop", true);
        var hidden = new TechStore.Client.Models.Catalog.CategoryOption(Guid.NewGuid(), null, "Cũ", "Thiết bị / Cũ", false);

        // Act & Assert
        Assert.Equal("Thiết bị / Laptop", active.Label);
        Assert.Equal("Thiết bị / Cũ (đang ẩn)", hidden.Label);
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

    [Fact]
    public async Task GetNodesAsync_MapsCountsAndChildren()
    {
        // Arrange
        const string json = """
            [{"id":"11111111-1111-1111-1111-111111111111","name":"Thiết bị","slug":"thiet-bi","description":"Mô tả","parentId":null,"isActive":true,"productCount":5,"skuCount":12,"subCategories":[{"id":"22222222-2222-2222-2222-222222222222","name":"Điện thoại","slug":"dien-thoai","description":null,"parentId":"11111111-1111-1111-1111-111111111111","isActive":false,"productCount":3,"skuCount":8,"subCategories":[]}]}]
            """;
        var repository = new CategoryRepository(new JsonApiClient(json));

        // Act
        var nodes = await repository.GetNodesAsync();

        // Assert
        var root = Assert.Single(nodes);
        Assert.Equal("Thiết bị", root.Name);
        Assert.Equal(5, root.ProductCount);
        Assert.Equal(12, root.SkuCount);
        var child = Assert.Single(root.Children);
        Assert.Equal("Điện thoại", child.Name);
        Assert.False(child.IsActive);
        Assert.Equal(3, child.ProductCount);
        Assert.Equal(8, child.SkuCount);
    }

    [Fact]
    public async Task CreateAsync_PostsToCategories()
    {
        // Arrange
        var api = new RecordingWorkspaceApiClient();
        var repository = new CategoryRepository(api);
        var request = new CreateCategoryRequest { Name = "Phụ kiện", Slug = "phu-kien" };

        // Act
        var dto = await repository.CreateAsync(request);

        // Assert
        Assert.Equal("api/categories", api.LastPath);
        Assert.Equal("Phụ kiện", dto.Name);
    }

    [Fact]
    public async Task UpdateAsync_PutsToCategoryId()
    {
        // Arrange
        var api = new RecordingWorkspaceApiClient();
        var repository = new CategoryRepository(api);
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var request = new UpdateCategoryRequest { Name = "Laptop", Slug = "laptop", IsActive = true };

        // Act
        var dto = await repository.UpdateAsync(id, request);

        // Assert
        Assert.Equal($"api/categories/{id:D}", api.LastPath);
        Assert.Equal(id, dto.Id);
    }

    [Fact]
    public async Task DeleteAsync_DeletesCategoryId()
    {
        // Arrange
        var api = new RecordingWorkspaceApiClient();
        var repository = new CategoryRepository(api);
        var id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        await repository.DeleteAsync(id);

        // Assert
        Assert.Equal($"api/categories/{id:D}", api.LastPath);
    }

    [Fact]
    public async Task CreateAsync_Http409_ThrowsConflictWithDetail()
    {
        // Arrange
        var error = new ApiException(ApiErrorKind.Http, "Conflict", statusCode: 409, detail: "Slug already in use.");
        var repository = new CategoryRepository(new ThrowingApiClient(error));

        // Act
        var ex = await Assert.ThrowsAsync<CategoryApiException>(() => repository.CreateAsync(new()));

        // Assert
        Assert.Equal(CategoryApiFailure.Conflict, ex.Failure);
        Assert.Equal("Slug already in use.", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_Http400_JoinsFieldErrors()
    {
        // Arrange
        var fields = new Dictionary<string, IReadOnlyList<string>>
        {
            ["name"] = ["Name is required."],
            ["slug"] = ["Invalid slug."]
        };
        var error = new ApiException(ApiErrorKind.Http, "Bad Request", statusCode: 400, fieldErrors: fields);
        var repository = new CategoryRepository(new ThrowingApiClient(error));

        // Act
        var ex = await Assert.ThrowsAsync<CategoryApiException>(() => repository.UpdateAsync(Guid.NewGuid(), new()));

        // Assert
        Assert.Equal(CategoryApiFailure.BadRequest, ex.Failure);
        Assert.Equal("Name is required.; Invalid slug.", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_Http401_ThrowsUnauthorized()
    {
        // Arrange
        var error = ApiException.Http(401, null);
        var repository = new CategoryRepository(new ThrowingApiClient(error));

        // Act
        var ex = await Assert.ThrowsAsync<CategoryApiException>(() => repository.DeleteAsync(Guid.NewGuid()));

        // Assert
        Assert.Equal(CategoryApiFailure.Unauthorized, ex.Failure);
    }

    [Fact]
    public async Task GetNodesAsync_NetworkError_ThrowsUnavailable()
    {
        // Arrange
        var error = new ApiException(ApiErrorKind.Network, "Network down");
        var repository = new CategoryRepository(new ThrowingApiClient(error));

        // Act
        var ex = await Assert.ThrowsAsync<CategoryApiException>(() => repository.GetNodesAsync());

        // Assert
        Assert.Equal(CategoryApiFailure.Unavailable, ex.Failure);
    }

    private sealed class RecordingWorkspaceApiClient : IApiClient
    {
        public string? LastPath { get; private set; }
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            var createReq = (CreateCategoryRequest)(object)request!;
            return Task.FromResult((TResponse)(object)new CategoryDto { Id = Guid.NewGuid(), Name = createReq.Name ?? string.Empty });
        }
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            var id = Guid.Parse(relativePath.Split('/')[^1]);
            return Task.FromResult((TResponse)(object)new CategoryDto { Id = id });
        }
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            return Task.CompletedTask;
        }
    }

    private sealed class JsonApiClient(string json) : IApiClient
    {
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            Assert.Equal("api/categories/tree", relativePath);
            using var document = JsonDocument.Parse(json);
            return Task.FromResult((TResponse)(object)document.RootElement.Clone());
        }
        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingApiClient(ApiException error) : IApiClient
    {
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default) => throw error;
        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw error;
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw error;
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw error;
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw error;
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw error;
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw error;
    }
}
