using TechStore.Api.Customers.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Services;

/// <summary>Whether a voucher applies to an order's goods and how much it takes off, in whole đồng.</summary>
public sealed record VoucherEvaluation(bool IsEligible, decimal Amount, string? Reason)
{
    public static VoucherEvaluation Eligible(decimal amount) => new(true, amount, null);
    public static VoucherEvaluation Refused(string reason) => new(false, 0m, reason);
}

/// <summary>
/// Voucher eligibility and amount (owner: Vy). <c>goodsAmount</c> is the goods value after line discounts,
/// the same base the order discount is taken from. Pure, so the order pricing and the voucher service agree.
/// </summary>
public static class VoucherRules
{
    public static VoucherEvaluation Evaluate(Voucher voucher, decimal goodsAmount, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(voucher);

        if (!voucher.IsActive)
            return VoucherEvaluation.Refused("Voucher is not active.");
        if (voucher.ExpiresAt <= nowUtc)
            return VoucherEvaluation.Refused("Voucher has expired.");
        if (goodsAmount < voucher.MinOrderAmount)
            return VoucherEvaluation.Refused($"Order must be at least {voucher.MinOrderAmount:N0} to use this voucher.");
        if (voucher.UsedCount + voucher.ReservedCount >= voucher.UsageLimit)
            return VoucherEvaluation.Refused("Voucher has no uses left.");

        var amount = voucher.DiscountType == VoucherType.Percentage
            ? decimal.Round(goodsAmount * voucher.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
            : decimal.Round(voucher.DiscountValue, 0, MidpointRounding.AwayFromZero);

        if (voucher.DiscountType == VoucherType.Percentage && voucher.MaxDiscountAmount.HasValue)
            amount = Math.Min(amount, voucher.MaxDiscountAmount.Value);

        return VoucherEvaluation.Eligible(Math.Min(amount, goodsAmount));
    }
}
