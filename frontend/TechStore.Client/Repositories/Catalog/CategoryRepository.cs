using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;

namespace TechStore.Client.Repositories.Catalog;

public sealed class CategoryRepository(IApiClient apiClient) : ICategoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CategoryOption>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var json = await apiClient.GetAsync<JsonElement>("api/categories/tree", cancellationToken);
        ValidateTree(json);

        IReadOnlyList<CategoryTreeNodeDto> nodes;
        try
        {
            nodes = json.Deserialize<List<CategoryTreeNodeDto>>(JsonOptions)
                ?? throw InvalidResponse("The category tree response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The category tree response did not match the API contract.", exception);
        }

        var result = new List<CategoryOption>();
        var ids = new HashSet<Guid>();
        foreach (var node in nodes) Flatten(node, [], result, ids);
        return result;
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
        result.Add(new CategoryOption(node.Id, node.ParentId, node.Name, string.Join(" / ", segments)));
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
