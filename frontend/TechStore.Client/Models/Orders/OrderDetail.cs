using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Orders;

/// <summary>One order line as the server priced it. There is no unit cost: cost never reaches the client.</summary>
public sealed record OrderLine(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    Guid? SerialImeiId,
    string? Imei,
    string? SerialNumber,
    string ProductName,
    string? VariantName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal TotalPrice);

public sealed record OrderDetail(
    Guid Id,
    string OrderCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CompletedAt,
    IReadOnlyList<OrderLine> Lines);
