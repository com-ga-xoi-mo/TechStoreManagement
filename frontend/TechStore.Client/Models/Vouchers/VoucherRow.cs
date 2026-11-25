namespace TechStore.Client.Models.Vouchers;

/// <summary>A voucher with every string the list row shows already worked out, so the view only binds.</summary>
public sealed record VoucherRow(
    VoucherItem Voucher,
    string Code,
    string Description,
    string DiscountLabel,
    string MinOrderLabel,
    string UsageLabel,
    string ExpiryLabel,
    string StatusLabel,
    string StatusTone,
    bool CanActivate,
    bool CanDeactivate)
{
    public static VoucherRow From(VoucherItem voucher, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        var status = VoucherPresentation.StatusOf(voucher, nowUtc);
        return new(
            voucher,
            voucher.Code,
            string.IsNullOrWhiteSpace(voucher.Description) ? "—" : voucher.Description,
            VoucherPresentation.DiscountLabel(voucher),
            VoucherPresentation.MinOrderLabel(voucher),
            VoucherPresentation.UsageLabel(voucher),
            VoucherPresentation.ExpiryLabel(voucher),
            VoucherPresentation.StatusLabel(status),
            status switch
            {
                VoucherStatus.Active => "Success",
                VoucherStatus.Expired => "Danger",
                VoucherStatus.Exhausted => "Warning",
                _ => "Neutral"
            },
            CanActivate: !voucher.IsActive,
            CanDeactivate: voucher.IsActive);
    }
}
