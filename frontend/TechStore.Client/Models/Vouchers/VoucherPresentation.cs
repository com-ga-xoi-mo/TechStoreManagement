using System.Globalization;

namespace TechStore.Client.Models.Vouchers;

public enum VoucherStatus
{
    Active,
    Inactive,
    Expired,
    Exhausted
}

/// <summary>Display rules for vouchers. Pure, so the list view only binds to the strings.</summary>
public static class VoucherPresentation
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Switched off wins over expired, expired over exhausted. Held uses count as taken.</summary>
    public static VoucherStatus StatusOf(VoucherItem voucher, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        if (!voucher.IsActive) return VoucherStatus.Inactive;
        if (voucher.ExpiresAt <= nowUtc) return VoucherStatus.Expired;
        if (voucher.UsedCount + voucher.ReservedCount >= voucher.UsageLimit) return VoucherStatus.Exhausted;
        return VoucherStatus.Active;
    }

    public static string StatusLabel(VoucherStatus status) => status switch
    {
        VoucherStatus.Inactive => "Đã tắt",
        VoucherStatus.Expired => "Hết hạn",
        VoucherStatus.Exhausted => "Hết lượt",
        _ => "Đang hoạt động"
    };

    public static string DiscountLabel(VoucherItem voucher)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        if (!voucher.IsPercentage) return Money(voucher.DiscountValue);
        var percent = $"{voucher.DiscountValue.ToString("0.##", Vietnamese)}%";
        return voucher.MaxDiscountAmount is { } cap ? $"{percent} · tối đa {Money(cap)}" : percent;
    }

    public static string MinOrderLabel(VoucherItem voucher)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        return voucher.MinOrderAmount > 0 ? Money(voucher.MinOrderAmount) : "Không yêu cầu";
    }

    public static string UsageLabel(VoucherItem voucher)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        return voucher.ReservedCount > 0
            ? $"{voucher.UsedCount} đã dùng · {voucher.ReservedCount} đang giữ / {voucher.UsageLimit} lượt"
            : $"{voucher.UsedCount} / {voucher.UsageLimit} lượt";
    }

    public static string ExpiryLabel(VoucherItem voucher)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        return voucher.ExpiresAt.ToLocalTime().ToString("dd/MM/yyyy", Vietnamese);
    }

    private static string Money(decimal value) => $"{value.ToString("N0", Vietnamese)} ₫";
}
