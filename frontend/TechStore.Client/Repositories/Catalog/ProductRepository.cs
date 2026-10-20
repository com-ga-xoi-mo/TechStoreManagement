using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Catalog;

public sealed class ProductRepository(IApiClient apiClient) : IProductRepository, IProductCommandRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var relativePath = ProductQueryUri.Build(query);
        var json = await apiClient.GetAsync<JsonElement>(relativePath, cancellationToken);
        ValidatePageShape(json);

        PagedResultDto<ProductSummaryDto> response;
        try
        {
            response = json.Deserialize<PagedResultDto<ProductSummaryDto>>(JsonOptions)
                ?? throw InvalidResponse("The product page response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The product page response did not match the API contract.", exception);
        }

        ValidatePageMetadata(response);
        var items = response.Items.Select(MapSummary).ToArray();
        return new ProductPage(items, response.Page, response.PageSize, response.TotalCount, response.TotalPages);
    }

    public async Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("A product id is required.", nameof(id));

        JsonElement json;
        try
        {
            json = await apiClient.GetAsync<JsonElement>($"api/products/{id:D}", cancellationToken);
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Http && exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }

        return ParseDetail(json, id);
    }

    public async Task<ProductDetail> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PostAsync<CreateProductRequest, JsonElement>("api/products", request, cancellationToken);
        return ParseDetail(json, null);
    }

    public async Task<ProductDetail> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("A product id is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PutAsync<UpdateProductRequest, JsonElement>($"api/products/{id:D}", request, cancellationToken);
        return ParseDetail(json, id);
    }

    public async Task<ProductDetail> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("A product id is required.", nameof(id));
        var json = await apiClient.PatchAsync<UpdateProductStatusRequest, JsonElement>(
            $"api/products/{id:D}/status",
            new UpdateProductStatusRequest { IsActive = isActive },
            cancellationToken);
        return ParseDetail(json, id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("A product id is required.", nameof(id));
        await apiClient.DeleteAsync($"api/products/{id:D}", cancellationToken);
    }

    private static ProductDetail ParseDetail(JsonElement json, Guid? expectedId)
    {
        ValidateDetailShape(json);
        ProductDto response;
        try
        {
            response = json.Deserialize<ProductDto>(JsonOptions)
                ?? throw InvalidResponse("The product detail response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The product detail response did not match the API contract.", exception);
        }

        if (expectedId is { } id && response.Id != id)
            throw InvalidResponse("The product detail id did not match the requested id.");

        ValidateDetail(response);
        return MapDetail(response);
    }

    private static ProductSummary MapSummary(ProductSummaryDto dto)
    {
        if (dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Name)
            || string.IsNullOrWhiteSpace(dto.Sku) || string.IsNullOrWhiteSpace(dto.Brand)
            || dto.CategoryId == Guid.Empty || string.IsNullOrWhiteSpace(dto.CategoryName)
            || dto.PriceFrom < 0 || dto.PriceTo < dto.PriceFrom || dto.VariantCount < 0)
            throw InvalidResponse("A product summary contains invalid values.");

        return new ProductSummary(dto.Id, dto.Name, dto.Sku, dto.Brand, dto.CategoryId,
            dto.CategoryName, dto.PriceFrom, dto.PriceTo, dto.VariantCount,
            dto.IsSerialTracked, dto.IsActive, dto.ImageUrl);
    }

    private static ProductDetail MapDetail(ProductDto dto) => new(
        dto.Id, dto.CategoryId, dto.CategoryName, dto.Name, dto.Sku, dto.Barcode,
        dto.Brand, dto.BasePrice, dto.CostPrice, dto.Description, dto.ImageUrl,
        CloneSpecs(dto.Specs), dto.IsSerialTracked, dto.IsActive, dto.CreatedAt,
        dto.UpdatedAt, dto.Variants.Select(MapVariant).ToArray());

    private static ProductVariant MapVariant(ProductVariantDto dto) => new(
        dto.Id, dto.ProductId, dto.VariantName, dto.Sku, dto.Barcode, dto.Price,
        CloneSpecs(dto.Specs), CloneSpecs(dto.EffectiveSpecs), dto.IsActive,
        dto.CreatedAt, dto.UpdatedAt);

    private static IReadOnlyDictionary<string, JsonElement> CloneSpecs(
        IReadOnlyDictionary<string, JsonElement> specs) => specs.ToDictionary(
        pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);

    private static void ValidatePageShape(JsonElement json)
    {
        RequireObject(json, "product page");
        Require(json, "items", JsonValueKind.Array);
        RequireInt(json, "page");
        RequireInt(json, "pageSize");
        RequireInt(json, "totalCount");
        RequireInt(json, "totalPages");
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            RequireObject(item, "product summary");
            RequireGuid(item, "id");
            RequireString(item, "name");
            RequireString(item, "sku");
            RequireString(item, "brand");
            RequireGuid(item, "categoryId");
            RequireString(item, "categoryName");
            RequireDecimal(item, "priceFrom");
            RequireDecimal(item, "priceTo");
            RequireInt(item, "variantCount");
            RequireBoolean(item, "isSerialTracked");
            RequireBoolean(item, "isActive");
            RequireNullableString(item, "imageUrl");
        }
    }

    private static void ValidateDetailShape(JsonElement json)
    {
        RequireObject(json, "product detail");
        RequireGuid(json, "id");
        RequireGuid(json, "categoryId");
        RequireString(json, "categoryName");
        RequireString(json, "name");
        RequireString(json, "sku");
        RequireNullableString(json, "barcode");
        RequireString(json, "brand");
        RequireDecimal(json, "basePrice");
        RequireDecimal(json, "costPrice");
        RequireNullableString(json, "description");
        RequireNullableString(json, "imageUrl");
        RequireSpecs(json, "specs");
        RequireBoolean(json, "isSerialTracked");
        RequireBoolean(json, "isActive");
        RequireDateTime(json, "createdAt");
        RequireNullableDateTime(json, "updatedAt");
        Require(json, "variants", JsonValueKind.Array);
        foreach (var variant in json.GetProperty("variants").EnumerateArray())
        {
            RequireObject(variant, "product variant");
            RequireGuid(variant, "id");
            RequireGuid(variant, "productId");
            RequireString(variant, "variantName");
            RequireString(variant, "sku");
            RequireNullableString(variant, "barcode");
            RequireDecimal(variant, "price");
            RequireSpecs(variant, "specs");
            RequireSpecs(variant, "effectiveSpecs");
            RequireBoolean(variant, "isActive");
            RequireDateTime(variant, "createdAt");
            RequireNullableDateTime(variant, "updatedAt");
        }
    }

    private static void ValidatePageMetadata(PagedResultDto<ProductSummaryDto> page)
    {
        if (page.Items is null || page.Page < 1 || page.PageSize is < 1 or > 100
            || page.TotalCount < 0 || page.TotalPages < 0)
            throw InvalidResponse("Product page metadata is outside the API contract.");

        var expectedPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)page.TotalCount / page.PageSize);
        if (page.TotalPages != expectedPages)
            throw InvalidResponse("Product page totalPages does not match totalCount and pageSize.");
    }

    private static void ValidateDetail(ProductDto product)
    {
        if (product.Id == Guid.Empty || product.CategoryId == Guid.Empty
            || string.IsNullOrWhiteSpace(product.Name) || string.IsNullOrWhiteSpace(product.Sku)
            || string.IsNullOrWhiteSpace(product.Brand) || product.BasePrice < 0 || product.CostPrice < 0)
            throw InvalidResponse("The product detail contains invalid values.");

        ValidateSpecs(product.Specs);
        foreach (var variant in product.Variants)
        {
            if (variant.Id == Guid.Empty || variant.ProductId != product.Id
                || string.IsNullOrWhiteSpace(variant.VariantName) || string.IsNullOrWhiteSpace(variant.Sku)
                || variant.Price < 0)
                throw InvalidResponse("A product variant contains invalid values.");
            ValidateSpecs(variant.Specs);
            ValidateSpecs(variant.EffectiveSpecs);
        }
    }

    private static void ValidateSpecs(IReadOnlyDictionary<string, JsonElement> specs)
    {
        foreach (var (key, value) in specs)
        {
            if (string.IsNullOrWhiteSpace(key)
                || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                throw InvalidResponse("Product specification values must be strings, numbers, or booleans.");
        }
    }

    private static void RequireObject(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Object) throw InvalidResponse($"The {description} must be a JSON object.");
    }

    private static JsonElement Require(JsonElement value, string property, JsonValueKind kind)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != kind)
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
        return result;
    }

    private static void RequireString(JsonElement value, string property) => _ = Require(value, property, JsonValueKind.String);

    private static void RequireNullableString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
    }

    private static void RequireBoolean(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
    }

    private static void RequireGuid(JsonElement value, string property)
    {
        var field = Require(value, property, JsonValueKind.String);
        if (!Guid.TryParse(field.GetString(), out var id) || id == Guid.Empty)
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    private static void RequireInt(JsonElement value, string property)
    {
        if (!Require(value, property, JsonValueKind.Number).TryGetInt32(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    private static void RequireDecimal(JsonElement value, string property)
    {
        if (!Require(value, property, JsonValueKind.Number).TryGetDecimal(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    private static void RequireDateTime(JsonElement value, string property)
    {
        if (!Require(value, property, JsonValueKind.String).TryGetDateTime(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    private static void RequireNullableDateTime(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
            || (result.ValueKind == JsonValueKind.String && !result.TryGetDateTime(out _)))
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
    }

    private static void RequireSpecs(JsonElement value, string property)
    {
        var specs = Require(value, property, JsonValueKind.Object);
        foreach (var pair in specs.EnumerateObject())
        {
            if (pair.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                throw InvalidResponse($"The API response has an invalid '{property}' value.");
        }
    }

    private static ApiException InvalidResponse(string message, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, message, innerException: inner);
}
