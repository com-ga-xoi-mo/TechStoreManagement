namespace TechStore.Client.Models.Vouchers;

/// <summary>One voucher as the management list shows it. <see cref="DiscountType"/> is <c>Percentage</c> or <c>FixedAmount</c>.</summary>
public sealed record VoucherItem(
    Guid Id,
    string Code,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    int UsageLimit,
    int UsedCount,
    int ReservedCount,
    DateTime ExpiresAt,
    bool IsActive,
    DateTime CreatedAt)
{
    public const string Percentage = "Percentage";
    public const string FixedAmount = "FixedAmount";

    public bool IsPercentage => DiscountType == Percentage;
}

public sealed record VoucherPage(
    IReadOnlyList<VoucherItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record VoucherQuery
{
    public string? Query { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
