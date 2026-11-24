namespace TechStore.Shared.DTOs;

public record ReturnBalanceDto(Guid OrderItemId, string ProductName, Guid? SerialImeiId,
    string? SerialNumber, int RemainingQuantity, decimal RemainingAmount, string? Sku = null, string? VariantName = null);
public record ReturnLineDto(Guid OrderItemId, int Quantity, decimal NetAmount, decimal TaxAmount, decimal DiscountAmount);
public record ReturnDto(Guid Id, Guid OrderId, string Status, string Reason, string? Method,
    string? BankReference, Guid? ReplacementOrderId, DateTime CreatedAt, DateTime? RefundedAt, List<ReturnLineDto> Lines, DateTime? BankApprovedAt = null);
public record OrderReturnsDto(Guid OrderId, string OrderCode, string OrderStatus,
    List<ReturnBalanceDto> Balances, List<ReturnDto> History,
    List<LegacyReturnDto>? LegacyRefunds = null, string? ReviewReason = null);
public record LegacyReturnDto(Guid Id, Guid OrderItemId, int Quantity, decimal RefundAmount,
    string Method, DateTime RefundedAt, string Reason, bool IsRestocked);
public record ReturnInspectionDto(Guid ReturnLineId, Guid RequestId, string ProductName,
    Guid? SerialImeiId, string? SerialNumber, int Quantity, int Pending, int Restocked, int Defective, int UnderRepair, string? Sku = null);
