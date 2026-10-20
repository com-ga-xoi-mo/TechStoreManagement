using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Customers;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Customers;

public sealed class CustomerRepository(IApiClient apiClient) : ICustomerRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<CustomerPage<CustomerSummary>> SearchAsync(CustomerQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return GetPageAsync<CustomerSummaryDto, CustomerSummary>(
            CustomerQueryUri.Build(query), ValidateCustomerSummary, MapCustomerSummary, cancellationToken);
    }

    public async Task<CustomerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureId(id, nameof(id));
        JsonElement json;
        try
        {
            json = await apiClient.GetAsync<JsonElement>($"api/customers/{id:D}", cancellationToken);
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Http && exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }

        ValidateCustomerProfile(json);
        var profile = MapCustomerProfile(Deserialize<CustomerDto>(json, "customer profile"));
        if (profile.Id != id) throw InvalidResponse("The customer profile id did not match the requested id.");
        return profile;
    }

    public Task<CustomerPage<CustomerLoyaltyTransaction>> GetLoyaltyHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetPageAsync<CustomerLoyaltyPointDto, CustomerLoyaltyTransaction>(
            BuildHistoryPath(customerId, "loyalty-points", page, pageSize), ValidateLoyaltyPoint, dto =>
                new(dto.Id, dto.PointsChange, dto.BalanceAfter, dto.Reason, dto.OrderId, dto.CreatedAt), cancellationToken);

    public Task<CustomerPage<CustomerOrder>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetPageAsync<OrderSummaryDto, CustomerOrder>(
            BuildHistoryPath(customerId, "orders", page, pageSize), ValidateOrderSummary, dto =>
                new(dto.Id, dto.OrderCode, dto.Status, dto.PaymentMethod, dto.TotalAmount, dto.ItemCount,
                    dto.CreatedAt, dto.PaidAt, dto.CompletedAt, dto.PrimaryProductName, dto.PrimaryVariantName,
                    dto.PrimaryCategoryName), cancellationToken);

    public Task<CustomerPage<CustomerPurchasedItem>> GetPurchasedItemsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetPageAsync<CustomerPurchasedItemDto, CustomerPurchasedItem>(
            BuildHistoryPath(customerId, "purchased-items", page, pageSize), ValidatePurchasedItem, dto =>
                new(dto.Id, dto.OrderId, dto.OrderCode, dto.CompletedAt, dto.ProductId, dto.VariantId,
                    dto.ProductName, dto.VariantName, dto.Sku, dto.SerialImeiId, dto.SerialNumber, dto.Imei,
                    dto.Quantity, dto.TotalPrice, dto.WarrantyStartAt, dto.WarrantyEndAt, dto.DeviceStatus,
                    dto.CategoryName), cancellationToken);

    public async Task<CustomerProfile> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PostAsync<CreateCustomerRequest, JsonElement>("api/customers", request, cancellationToken);
        ValidateCustomerProfile(json);
        return MapCustomerProfile(Deserialize<CustomerDto>(json, "created customer profile"));
    }

    public async Task<CustomerProfile> UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(customerId, nameof(customerId));
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PutAsync<UpdateCustomerRequest, JsonElement>($"api/customers/{customerId:D}", request, cancellationToken);
        ValidateCustomerProfile(json);
        var profile = MapCustomerProfile(Deserialize<CustomerDto>(json, "updated customer profile"));
        if (profile.Id != customerId) throw InvalidResponse("The customer profile id did not match the requested id.");
        return profile;
    }

    public Task DeactivateAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        EnsureId(customerId, nameof(customerId));
        return apiClient.PatchAsync($"api/customers/{customerId:D}/deactivate", cancellationToken);
    }

    public Task ActivateAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        EnsureId(customerId, nameof(customerId));
        return apiClient.PatchAsync($"api/customers/{customerId:D}/activate", cancellationToken);
    }

    private async Task<CustomerPage<TModel>> GetPageAsync<TDto, TModel>(
        string path,
        Action<JsonElement> validateItem,
        Func<TDto, TModel> map,
        CancellationToken cancellationToken)
        where TDto : class
    {
        var json = await apiClient.GetAsync<JsonElement>(path, cancellationToken);
        RequireObject(json, "page");
        var items = Require(json, "items", JsonValueKind.Array);
        RequireInt(json, "page");
        RequireInt(json, "pageSize");
        RequireInt(json, "totalCount");
        RequireInt(json, "totalPages");
        foreach (var item in items.EnumerateArray())
        {
            RequireObject(item, "page item");
            validateItem(item);
        }

        var response = Deserialize<PagedResultDto<TDto>>(json, "page");
        if (response.Page < 1 || response.PageSize is < 1 or > 100 || response.TotalCount < 0 || response.Items is null)
            throw InvalidResponse("Customer page metadata is outside the API contract.");
        var expectedTotalPages = response.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)response.TotalCount / response.PageSize);
        if (response.TotalPages != expectedTotalPages)
            throw InvalidResponse("Customer page totalPages does not match totalCount and pageSize.");
        return new(response.Items.Select(map).ToArray(), response.Page, response.PageSize, response.TotalCount, response.TotalPages);
    }

    private static CustomerSummary MapCustomerSummary(CustomerSummaryDto dto) => new(
        dto.Id, dto.FullName, dto.Phone, dto.LoyaltyPoints, dto.Tier, dto.IsActive, dto.CreatedAt,
        dto.OrderCount, dto.TotalSpent, dto.LastPurchaseAt);

    private static CustomerProfile MapCustomerProfile(CustomerDto dto) => new(
        dto.Id, dto.FullName, dto.Phone, dto.Email, dto.DateOfBirth, dto.Address, dto.LoyaltyPoints,
        dto.Tier, dto.IsActive, dto.CreatedAt, dto.UpdatedAt, dto.OrderCount, dto.TotalSpent, dto.LastPurchaseAt,
        dto.CompletedOrderCount, dto.CancelledOrderCount);

    private static void ValidateCustomerSummary(JsonElement json)
    {
        RequireGuid(json, "id");
        RequireString(json, "fullName");
        RequireString(json, "phone");
        RequireInt(json, "loyaltyPoints");
        RequireString(json, "tier");
        RequireBoolean(json, "isActive");
        RequireDateTime(json, "createdAt");
        RequireInt(json, "orderCount");
        RequireDecimal(json, "totalSpent");
        RequireNullableDateTime(json, "lastPurchaseAt");
        if (string.IsNullOrWhiteSpace(json.GetProperty("fullName").GetString())
            || string.IsNullOrWhiteSpace(json.GetProperty("phone").GetString())
            || string.IsNullOrWhiteSpace(json.GetProperty("tier").GetString())
            || json.GetProperty("loyaltyPoints").GetInt32() < 0
            || json.GetProperty("orderCount").GetInt32() < 0
            || json.GetProperty("totalSpent").GetDecimal() < 0)
            throw InvalidResponse("A customer summary contains invalid values.");
    }

    private static void ValidateCustomerProfile(JsonElement json)
    {
        ValidateCustomerSummary(json);
        RequireNullableString(json, "email");
        Require(json, "dateOfBirth", JsonValueKind.String, JsonValueKind.Null);
        RequireNullableString(json, "address");
        RequireNullableDateTime(json, "updatedAt");
    }

    private static void ValidateLoyaltyPoint(JsonElement json)
    {
        RequireGuid(json, "id");
        RequireInt(json, "pointsChange");
        RequireInt(json, "balanceAfter");
        RequireString(json, "reason");
        RequireNullableGuid(json, "orderId");
        RequireDateTime(json, "createdAt");
    }

    private static void ValidateOrderSummary(JsonElement json)
    {
        RequireGuid(json, "id");
        RequireString(json, "orderCode");
        RequireString(json, "status");
        RequireString(json, "paymentMethod");
        RequireNullableGuid(json, "customerId");
        RequireNullableString(json, "customerName");
        RequireDecimal(json, "totalAmount");
        RequireInt(json, "itemCount");
        RequireDateTime(json, "createdAt");
        RequireNullableDateTime(json, "paidAt");
        RequireNullableDateTime(json, "completedAt");
    }

    private static void ValidatePurchasedItem(JsonElement json)
    {
        RequireGuid(json, "id");
        RequireGuid(json, "orderId");
        RequireString(json, "orderCode");
        RequireNullableDateTime(json, "completedAt");
        RequireGuid(json, "productId");
        RequireNullableGuid(json, "variantId");
        RequireString(json, "productName");
        RequireNullableString(json, "categoryName");
        RequireNullableString(json, "variantName");
        RequireString(json, "sku");
        RequireNullableGuid(json, "serialImeiId");
        RequireNullableString(json, "serialNumber");
        RequireNullableString(json, "imei");
        RequireInt(json, "quantity");
        RequireDecimal(json, "totalPrice");
        if (string.IsNullOrWhiteSpace(json.GetProperty("orderCode").GetString())
            || string.IsNullOrWhiteSpace(json.GetProperty("productName").GetString())
            || string.IsNullOrWhiteSpace(json.GetProperty("sku").GetString())
            || json.GetProperty("quantity").GetInt32() < 1
            || json.GetProperty("totalPrice").GetDecimal() < 0)
            throw InvalidResponse("A purchased item contains invalid values.");
    }

    private static string BuildHistoryPath(Guid customerId, string resource, int page, int pageSize)
    {
        EnsureId(customerId, nameof(customerId));
        return $"api/customers/{customerId:D}/{resource}?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 100)}";
    }

    private static T Deserialize<T>(JsonElement json, string description) where T : class
    {
        try
        {
            return json.Deserialize<T>(JsonOptions) ?? throw InvalidResponse($"The {description} response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse($"The {description} response did not match the API contract.", exception);
        }
    }

    private static void RequireObject(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Object) throw InvalidResponse($"The {description} must be a JSON object.");
    }

    private static JsonElement Require(JsonElement value, string property, params JsonValueKind[] kinds)
    {
        if (!value.TryGetProperty(property, out var result) || !kinds.Contains(result.ValueKind))
            throw InvalidResponse($"The API response is missing or has an invalid '{property}' field.");
        return result;
    }

    private static void RequireString(JsonElement value, string property) => _ = Require(value, property, JsonValueKind.String);
    private static void RequireNullableString(JsonElement value, string property) => _ = Require(value, property, JsonValueKind.String, JsonValueKind.Null);
    private static void RequireBoolean(JsonElement value, string property) => _ = Require(value, property, JsonValueKind.True, JsonValueKind.False);

    private static void RequireGuid(JsonElement value, string property)
    {
        if (!Guid.TryParse(Require(value, property, JsonValueKind.String).GetString(), out var id) || id == Guid.Empty)
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    private static void RequireNullableGuid(JsonElement value, string property)
    {
        var field = Require(value, property, JsonValueKind.String, JsonValueKind.Null);
        if (field.ValueKind == JsonValueKind.String && (!Guid.TryParse(field.GetString(), out var id) || id == Guid.Empty))
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
        var field = Require(value, property, JsonValueKind.String, JsonValueKind.Null);
        if (field.ValueKind == JsonValueKind.String && !field.TryGetDateTime(out _))
            throw InvalidResponse($"The API response has an invalid '{property}' field.");
    }

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty) throw new ArgumentException("A customer id is required.", parameterName);
    }

    private static ApiException InvalidResponse(string message, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, message, innerException: inner);
}
