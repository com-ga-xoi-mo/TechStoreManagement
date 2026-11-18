using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Orders.Services;

public record PricedLine(decimal UnitPrice, int Quantity, decimal DiscountAmount, decimal TotalPrice);

public record OrderTotals(decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount);

/// <summary>One line's share of the order discount and of the order tax, in whole đồng.</summary>
public record LineAllocation(decimal OrderDiscountAmount, decimal TaxAmount);

/// <summary>Order totals plus one allocation per input line, in the same order.</summary>
public record OrderPricing(OrderTotals Totals, IReadOnlyList<LineAllocation> Lines);

/// <summary>
/// The only money formula (owner: Vy). Order: line discount, then order discount, then VAT.
/// Catalog prices exclude VAT. Computed amounts are whole đồng, half away from zero; shares are split with the
/// largest-remainder method so the lines always add up to the order amounts. Callers check permissions and limits.
/// </summary>
public static class OrderPricingHelper
{
    // Variant price when a variant is selected, otherwise the product base price.
    public static decimal GetUnitPrice(Product product, ProductVariant? variant) =>
        variant?.Price ?? product.BasePrice;

    public static PricedLine PriceLine(decimal unitPrice, int quantity, decimal discountAmount) =>
        new(unitPrice, quantity, discountAmount, unitPrice * quantity - discountAmount);

    /// <param name="lines">Lines already priced with <see cref="PriceLine"/>; their discounts are not applied again.</param>
    /// <param name="orderDiscount">Order-level discount, 0..subtotal.</param>
    /// <param name="taxRatePercent">VAT rate in percent, 0..100.</param>
    public static OrderPricing Calculate(IReadOnlyList<PricedLine> lines, decimal orderDiscount, decimal taxRatePercent)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegative(taxRatePercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(taxRatePercent, 100m);

        var subtotal = lines.Sum(l => l.TotalPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(orderDiscount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(orderDiscount, subtotal);

        var discountShares = Allocate(orderDiscount, lines.Select(l => l.TotalPrice).ToArray());
        var taxable = lines.Select((l, i) => l.TotalPrice - discountShares[i]).ToArray();
        var tax = RoundDong((subtotal - orderDiscount) * taxRatePercent / 100m);
        var taxShares = Allocate(tax, taxable);

        var totals = new OrderTotals(subtotal, orderDiscount, tax, subtotal - orderDiscount + tax);
        var allocations = lines.Select((_, i) => new LineAllocation(discountShares[i], taxShares[i])).ToArray();
        return new OrderPricing(totals, allocations);
    }

    /// <summary>True when <paramref name="totalDiscount"/> is at most <paramref name="maxPercent"/>% of the amount before discounts.</summary>
    public static bool IsWithinDiscountLimit(decimal grossAmount, decimal totalDiscount, decimal maxPercent) =>
        totalDiscount <= grossAmount * maxPercent / 100m;

    public static decimal RoundDong(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    // Splits a whole-đồng amount by weight: everyone gets the rounded-down share, then the remaining đồng go
    // one by one to the largest remainders (ties: earlier line). Zero weights get nothing.
    private static decimal[] Allocate(decimal amount, decimal[] weights)
    {
        var shares = new decimal[weights.Length];
        var totalWeight = weights.Sum();
        if (amount == 0m || totalWeight <= 0m) return shares;

        var remainders = new decimal[weights.Length];
        for (var i = 0; i < weights.Length; i++)
        {
            var exact = amount * weights[i] / totalWeight;
            shares[i] = decimal.Floor(exact);
            remainders[i] = exact - shares[i];
        }

        var left = (int)(amount - shares.Sum());
        foreach (var i in Enumerable.Range(0, weights.Length)
                     .Where(i => weights[i] > 0m)
                     .OrderByDescending(i => remainders[i])
                     .ThenBy(i => i)
                     .Take(left))
        {
            shares[i] += 1m;
        }

        return shares;
    }
}
