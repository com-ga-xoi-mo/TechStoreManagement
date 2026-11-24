namespace TechStore.Shared.DTOs;

public sealed record WarrantyLookupDto
{
    public Guid DeviceId { get; init; }
    public string? Imei { get; init; }
    public string? SerialNumber { get; init; }
    public string DeviceStatus { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public Guid? VariantId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? VariantName { get; init; }
    public DateTime? WarrantyStartAt { get; init; }
    public DateTime? WarrantyEndAt { get; init; }
    public bool Eligible { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public string? OrderCode { get; init; }
    public DateTime? PurchasedAt { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public bool? IsWalkIn { get; init; }
    public IReadOnlyList<WarrantyCaseSummaryDto> History { get; init; } = [];
}

public static class WarrantyReasonCodes
{
    public const string NotSold = nameof(NotSold);
    public const string NoCoveringSale = nameof(NoCoveringSale);
    public const string Expired = nameof(Expired);
    public const string LineReturned = nameof(LineReturned);
    public const string PendingReturn = nameof(PendingReturn);
    public const string OpenCase = nameof(OpenCase);
    public const string OrderCodeRequired = nameof(OrderCodeRequired);
    public const string OrderMismatch = nameof(OrderMismatch);
    public const string CustomerMismatch = nameof(CustomerMismatch);
}
