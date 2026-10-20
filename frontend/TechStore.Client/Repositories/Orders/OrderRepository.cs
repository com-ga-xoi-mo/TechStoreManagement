using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Orders;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using static TechStore.Client.Repositories.Orders.OrderJson;

namespace TechStore.Client.Repositories.Orders;

public sealed class OrderRepository(IApiClient apiClient) : IOrderRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderPage> SearchAsync(OrderQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var json = await apiClient.GetAsync<JsonElement>(OrderQueryUri.Build(query), cancellationToken);
        ValidatePageShape(json);

        PagedResultDto<OrderSummaryDto> response;
        try
        {
            response = json.Deserialize<PagedResultDto<OrderSummaryDto>>(JsonOptions)
                ?? throw InvalidResponse("The order page response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The order page response did not match the API contract.", exception);
        }

        ValidatePageMetadata(response);
        var items = response.Items.Select(MapSummary).ToArray();
        return new OrderPage(items, response.Page, response.PageSize, response.TotalCount, response.TotalPages);
    }

    public async Task<OrderDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("An order id is required.", nameof(id));

        JsonElement json;
        try
        {
            json = await apiClient.GetAsync<JsonElement>($"api/orders/{id:D}", cancellationToken);
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Http && exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }

        return ParseDetail(json, id);
    }

    public async Task<OrderDetail> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PostAsync<CreateOrderRequest, JsonElement>("api/orders", request, cancellationToken);
        return ParseDetail(json, expectedId: null);
    }

    public async Task<OrderDetail> UpdateStatusAsync(Guid id, OrderStatus target, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("An order id is required.", nameof(id));
        if (!Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown order status.");

        var body = new UpdateOrderStatusRequest { TargetStatus = target.ToString() };
        var json = await apiClient.PatchAsync<UpdateOrderStatusRequest, JsonElement>(
            $"api/orders/{id:D}/status", body, cancellationToken);
        return ParseDetail(json, id);
    }

    private static OrderDetail ParseDetail(JsonElement json, Guid? expectedId)
    {
        ValidateDetailShape(json);
        OrderDto dto;
        try
        {
            dto = json.Deserialize<OrderDto>(JsonOptions)
                ?? throw InvalidResponse("The order response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The order response did not match the API contract.", exception);
        }

        if (expectedId.HasValue && dto.Id != expectedId.Value)
            throw InvalidResponse("The order id did not match the requested id.");

        ValidateDetail(dto);
        return MapDetail(dto);
    }

    private static OrderSummary MapSummary(OrderSummaryDto dto)
    {
        if (dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.OrderCode)
            || dto.TotalAmount < 0 || dto.ItemCount < 0)
            throw InvalidResponse("An order summary contains invalid values.");

        return new OrderSummary(
            dto.Id,
            dto.OrderCode,
            ParseEnum<OrderStatus>(dto.Status, "status"),
            ParseEnum<PaymentMethod>(dto.PaymentMethod, "paymentMethod"),
            dto.CustomerId,
            dto.CustomerName,
            dto.TotalAmount,
            dto.ItemCount,
            dto.CreatedAt,
            dto.PaidAt,
            dto.CompletedAt);
    }

    private static OrderDetail MapDetail(OrderDto dto) => new(
        dto.Id,
        dto.OrderCode,
        ParseEnum<OrderStatus>(dto.Status, "status"),
        ParseEnum<PaymentMethod>(dto.PaymentMethod, "paymentMethod"),
        dto.CustomerId,
        dto.CustomerName,
        dto.CustomerPhone,
        dto.Subtotal,
        dto.DiscountAmount,
        dto.TaxAmount,
        dto.TotalAmount,
        dto.Notes,
        dto.CreatedAt,
        dto.PaidAt,
        dto.CompletedAt,
        dto.Items.Select(MapLine).ToArray());

    private static OrderLine MapLine(OrderItemDto dto) => new(
        dto.Id,
        dto.ProductId,
        dto.VariantId,
        dto.SerialImeiId,
        dto.Imei,
        dto.SerialNumber,
        dto.ProductName,
        dto.VariantName,
        dto.Sku,
        dto.UnitPrice,
        dto.Quantity,
        dto.DiscountAmount,
        dto.TotalPrice);

    private static void ValidatePageShape(JsonElement json)
    {
        RequireObject(json, "order page");
        Require(json, "items", JsonValueKind.Array);
        RequireInt(json, "page");
        RequireInt(json, "pageSize");
        RequireInt(json, "totalCount");
        RequireInt(json, "totalPages");
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            RequireObject(item, "order summary");
            RequireGuid(item, "id");
            RequireString(item, "orderCode");
            RequireString(item, "status");
            RequireString(item, "paymentMethod");
            RequireNullableGuid(item, "customerId");
            RequireNullableString(item, "customerName");
            RequireDecimal(item, "totalAmount");
            RequireInt(item, "itemCount");
            RequireDateTime(item, "createdAt");
            RequireNullableDateTime(item, "paidAt");
            RequireNullableDateTime(item, "completedAt");
        }
    }

    private static void ValidateDetailShape(JsonElement json)
    {
        RequireObject(json, "order");
        RequireGuid(json, "id");
        RequireString(json, "orderCode");
        RequireString(json, "status");
        RequireString(json, "paymentMethod");
        RequireNullableGuid(json, "customerId");
        RequireNullableString(json, "customerName");
        RequireNullableString(json, "customerPhone");
        RequireDecimal(json, "subtotal");
        RequireDecimal(json, "discountAmount");
        RequireDecimal(json, "taxAmount");
        RequireDecimal(json, "totalAmount");
        RequireNullableString(json, "notes");
        RequireDateTime(json, "createdAt");
        RequireNullableDateTime(json, "paidAt");
        RequireNullableDateTime(json, "completedAt");
        Require(json, "items", JsonValueKind.Array);
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            RequireObject(item, "order line");
            RequireGuid(item, "id");
            RequireGuid(item, "productId");
            RequireNullableGuid(item, "variantId");
            RequireNullableGuid(item, "serialImeiId");
            RequireNullableString(item, "imei");
            RequireNullableString(item, "serialNumber");
            RequireString(item, "productName");
            RequireNullableString(item, "variantName");
            RequireString(item, "sku");
            RequireDecimal(item, "unitPrice");
            RequireInt(item, "quantity");
            RequireDecimal(item, "discountAmount");
            RequireDecimal(item, "totalPrice");
        }
    }

    private static void ValidatePageMetadata(PagedResultDto<OrderSummaryDto> page)
    {
        if (page.Items is null || page.Page < 1 || page.PageSize is < 1 or > 100
            || page.TotalCount < 0 || page.TotalPages < 0)
            throw InvalidResponse("Order page metadata is outside the API contract.");

        var expectedPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)page.TotalCount / page.PageSize);
        if (page.TotalPages != expectedPages)
            throw InvalidResponse("Order page totalPages does not match totalCount and pageSize.");
    }

    private static void ValidateDetail(OrderDto order)
    {
        if (order.Id == Guid.Empty || string.IsNullOrWhiteSpace(order.OrderCode)
            || order.Subtotal < 0 || order.DiscountAmount < 0 || order.TaxAmount < 0 || order.TotalAmount < 0)
            throw InvalidResponse("The order contains invalid values.");

        foreach (var line in order.Items)
        {
            if (line.Id == Guid.Empty || line.ProductId == Guid.Empty
                || string.IsNullOrWhiteSpace(line.ProductName) || string.IsNullOrWhiteSpace(line.Sku)
                || line.Quantity < 1 || line.UnitPrice < 0 || line.DiscountAmount < 0 || line.TotalPrice < 0)
                throw InvalidResponse("An order line contains invalid values.");
        }
    }
}
