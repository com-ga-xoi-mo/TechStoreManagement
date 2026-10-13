using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public record CreateOrderResult(
    bool IsSuccess,
    OrderDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    string? ConflictReason,
    bool IsConflict)
{
    public static CreateOrderResult Success(OrderDto data) =>
        new(true, data, null, null, false);

    public static CreateOrderResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors, null, false);

    public static CreateOrderResult Conflict(string reason) =>
        new(false, null, null, reason, true);
}

public record OrderDetailResult(bool Found, OrderDto? Data)
{
    public static OrderDetailResult NotFound() => new(false, null);
    public static OrderDetailResult Success(OrderDto data) => new(true, data);
}

public record OrderSearchResult(
    bool IsSuccess,
    PagedResultDto<OrderSummaryDto>? Data,
    Dictionary<string, string[]>? ValidationErrors)
{
    public static OrderSearchResult Success(PagedResultDto<OrderSummaryDto> data) =>
        new(true, data, null);

    public static OrderSearchResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors);
}

public record OrderTransitionResult(
    bool IsSuccess,
    bool Found,
    OrderDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    string? ConflictReason,
    bool IsConflict)
{
    public static OrderTransitionResult Success(OrderDto data) =>
        new(true, true, data, null, null, false);

    public static OrderTransitionResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, true, null, errors, null, false);

    public static OrderTransitionResult NotFound() =>
        new(false, false, null, null, null, false);

    public static OrderTransitionResult Conflict(string reason) =>
        new(false, true, null, null, reason, true);
}

public interface IOrderService
{
    // UC-ORD-01: creates a Pending order (a draft that reserves nothing).
    Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request,
        Guid cashierUserId,
        CancellationToken cancellationToken = default);

    // UC-ORD-03
    Task<OrderDetailResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // UC-ORD-02
    Task<OrderSearchResult> SearchAsync(
        OrderSearchRequest request,
        CancellationToken cancellationToken = default);

    // UC-ORD-04
    Task<OrderTransitionResult> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default);
}
