using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Catalog;

public sealed class ProductImageRepository(IApiClient apiClient) : IProductImageRepository
{
    public async Task<ProductImage> UploadAsync(Guid productId, ImageUpload upload, CancellationToken cancellationToken = default)
    {
        EnsureId(productId);
        ArgumentNullException.ThrowIfNull(upload);
        var fields = upload.VariantId is { } variant ? new Dictionary<string, string> { ["variantId"] = variant.ToString("D") } : null;
        var json = await apiClient.PostFileAsync<JsonElement>($"api/products/{productId:D}/images",
            new("file", upload.FileName, upload.ContentType, upload.Content), fields, cancellationToken);
        return Parse(json);
    }

    public Task DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default)
    {
        EnsureId(productId); EnsureId(imageId);
        return apiClient.DeleteAsync($"api/products/{productId:D}/images/{imageId:D}", cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImage>> SaveLayoutAsync(Guid productId, IReadOnlyList<ImageLayoutEntry> entries, CancellationToken cancellationToken = default)
    {
        EnsureId(productId);
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries) EnsureId(entry.ImageId);
        var request = new UpdateProductImagesLayoutRequest
        {
            Items = entries.Select(i => new ProductImageLayoutItemRequest { Id = i.ImageId, VariantId = i.VariantId }).ToList()
        };
        var json = await apiClient.PutAsync<UpdateProductImagesLayoutRequest, JsonElement>($"api/products/{productId:D}/images/layout", request, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array) throw InvalidResponse();
        return json.EnumerateArray().Select(Parse).ToArray();
    }

    private static ProductImage Parse(JsonElement json)
    {
        try
        {
            if (json.ValueKind != JsonValueKind.Object) throw InvalidResponse();
            var id = json.GetProperty("id").GetGuid();
            var variant = json.GetProperty("variantId");
            Guid? variantId = variant.ValueKind == JsonValueKind.Null ? null : variant.GetGuid();
            var url = json.GetProperty("url").GetString();
            var contentType = json.GetProperty("contentType").GetString();
            var size = json.GetProperty("sizeBytes").GetInt32();
            var order = json.GetProperty("sortOrder").GetInt32();
            if (id == Guid.Empty || variantId == Guid.Empty || string.IsNullOrWhiteSpace(url)
                || contentType is not ("image/jpeg" or "image/png" or "image/webp") || size is <= 0 or > 5_242_880 || order < 0)
                throw InvalidResponse();
            return new(id, variantId, url, contentType, size, order);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw InvalidResponse(ex);
        }
    }

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("An id is required.", nameof(id));
    }
    private static ApiException InvalidResponse(Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, "The image response did not match the API contract.", innerException: inner);
}
