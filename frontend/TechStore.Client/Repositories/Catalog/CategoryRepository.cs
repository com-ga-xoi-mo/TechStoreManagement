using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Catalog;

public sealed class CategoryRepository(IApiClient apiClient) : ICategoryRepository, ICategoryWorkspaceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CategoryOption>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await LoadTreeAsync(cancellationToken);
        var result = new List<CategoryOption>();
        var ids = new HashSet<Guid>();
        foreach (var node in nodes) Flatten(node, [], result, ids);
        return result;
    }

    public async Task<IReadOnlyList<CategoryNode>> GetNodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var nodes = await LoadTreeAsync(cancellationToken);
            return nodes.Select(Map).ToArray();
        }
        catch (ApiException exception)
        {
            throw Translate(exception);
        }
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await apiClient.PostAsync<CreateCategoryRequest, CategoryDto>("api/categories", request, cancellationToken);
        }
        catch (ApiException exception)
        {
            throw Translate(
                exception,
                conflictDefault: "Danh mục hoặc slug đã tồn tại.",
                badRequestDefault: "Thông tin danh mục không hợp lệ.");
        }
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await apiClient.PutAsync<UpdateCategoryRequest, CategoryDto>($"api/categories/{id:D}", request, cancellationToken);
        }
        catch (ApiException exception)
        {
            throw Translate(
                exception,
                notFoundDefault: "Không tìm thấy danh mục cần sửa.",
                conflictDefault: "Danh mục hoặc slug đã tồn tại.",
                badRequestDefault: "Thông tin danh mục không hợp lệ.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await apiClient.DeleteAsync($"api/categories/{id:D}", cancellationToken);
        }
        catch (ApiException exception)
        {
            throw Translate(
                exception,
                notFoundDefault: "Không tìm thấy danh mục cần xóa.",
                conflictDefault: "Không thể xóa danh mục vì đang có sản phẩm liên kết.",
                badRequestDefault: "Không thể xóa danh mục vì vẫn còn danh mục con.");
        }
    }

    private async Task<IReadOnlyList<CategoryTreeNodeDto>> LoadTreeAsync(CancellationToken cancellationToken)
    {
        var json = await apiClient.GetAsync<JsonElement>("api/categories/tree", cancellationToken);
        ValidateTree(json);

        try
        {
            return json.Deserialize<List<CategoryTreeNodeDto>>(JsonOptions)
                ?? throw InvalidResponse("The category tree response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The category tree response did not match the API contract.", exception);
        }
    }

    private static CategoryNode Map(CategoryTreeNodeDto node) =>
        new(node.Id, node.Name, node.Slug, node.IsActive, node.SubCategories.Select(Map).ToArray(),
            ProductCount: node.ProductCount, SkuCount: node.SkuCount,
            Description: node.Description, ParentId: node.ParentId);

    private static CategoryApiException Translate(
        ApiException exception,
        string? notFoundDefault = null,
        string? conflictDefault = null,
        string? badRequestDefault = null)
    {
        if (exception.Kind == ApiErrorKind.InvalidResponse)
            return new CategoryApiException(CategoryApiFailure.InvalidResponse);
        if (exception.Kind is ApiErrorKind.Network or ApiErrorKind.Timeout)
            return new CategoryApiException(CategoryApiFailure.Unavailable);

        return exception.StatusCode switch
        {
            401 => new CategoryApiException(CategoryApiFailure.Unauthorized),
            403 => new CategoryApiException(CategoryApiFailure.Forbidden),
            404 => new CategoryApiException(
                CategoryApiFailure.NotFound,
                !string.IsNullOrWhiteSpace(exception.Detail) ? exception.Detail : notFoundDefault),
            409 => new CategoryApiException(
                CategoryApiFailure.Conflict,
                !string.IsNullOrWhiteSpace(exception.Detail) ? exception.Detail : conflictDefault),
            400 => new CategoryApiException(
                CategoryApiFailure.BadRequest,
                ResolveBadRequestMessage(exception, badRequestDefault)),
            _ => new CategoryApiException(CategoryApiFailure.Unavailable)
        };
    }

    private static string? ResolveBadRequestMessage(ApiException exception, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(exception.Detail))
            return exception.Detail;

        if (exception.FieldErrors.Count > 0)
        {
            var joined = string.Join("; ", exception.FieldErrors.Values
                .SelectMany(messages => messages)
                .Where(message => !string.IsNullOrWhiteSpace(message)));
            if (!string.IsNullOrWhiteSpace(joined))
                return joined;
        }

        return fallback;
    }

    private static void Flatten(
        CategoryTreeNodeDto node,
        IReadOnlyList<string> ancestors,
        ICollection<CategoryOption> result,
        ISet<Guid> ids)
    {
        if (node.Id == Guid.Empty || string.IsNullOrWhiteSpace(node.Name) || !ids.Add(node.Id))
            throw InvalidResponse("The category tree contains invalid identity or hierarchy data.");

        var segments = ancestors.Append(node.Name.Trim()).ToArray();
        result.Add(new CategoryOption(node.Id, node.ParentId, node.Name, string.Join(" / ", segments), node.IsActive));
        foreach (var child in node.SubCategories) Flatten(child, segments, result, ids);
    }

    private static void ValidateTree(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
            throw InvalidResponse("The category tree must be a JSON array.");
        foreach (var node in json.EnumerateArray()) ValidateNode(node, null, false);
    }

    private static void ValidateNode(JsonElement node, Guid? expectedParent, bool isNested)
    {
        if (node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("id", out var idElement)
            || idElement.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idElement.GetString(), out var id) || id == Guid.Empty
            || !node.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(name.GetString())
            || !node.TryGetProperty("slug", out var slug) || slug.ValueKind != JsonValueKind.String
            || !node.TryGetProperty("isActive", out var isActive) || isActive.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !node.TryGetProperty("parentId", out var parentValue)
            || (parentValue.ValueKind != JsonValueKind.Null
                && (parentValue.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(parentValue.GetString(), out var actualParent) || actualParent == Guid.Empty))
            || (isNested && (parentValue.ValueKind != JsonValueKind.String
                || !Guid.TryParse(parentValue.GetString(), out var nestedParent) || nestedParent != expectedParent)))
            throw InvalidResponse("The category tree contains a missing or invalid field.");

        if (!node.TryGetProperty("subCategories", out var children) || children.ValueKind != JsonValueKind.Array)
            throw InvalidResponse("The category tree contains an invalid subCategories field.");
        foreach (var child in children.EnumerateArray()) ValidateNode(child, id, true);
    }

    private static ApiException InvalidResponse(string message, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, message, innerException: inner);
}
