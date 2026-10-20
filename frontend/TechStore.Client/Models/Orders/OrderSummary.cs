using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Orders;

public sealed record OrderSummary(
    Guid Id,
    string OrderCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    Guid? CustomerId,
    string? CustomerName,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CompletedAt);

public sealed record OrderPage(
    IReadOnlyList<OrderSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
