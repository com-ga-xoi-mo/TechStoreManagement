using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Orders.Services;

public record PricedLine(decimal UnitPrice, int Quantity, decimal DiscountAmount, decimal TotalPrice);

public record OrderTotals(decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount);

public static class OrderPricingHelper
{
    // Variant price when a variant is selected, otherwise the product base price.
    public static decimal GetUnitPrice(Product product, ProductVariant? variant) =>
        variant?.Price ?? product.BasePrice;

    public static PricedLine PriceLine(decimal unitPrice, int quantity, decimal discountAmount) =>
        new(unitPrice, quantity, discountAmount, unitPrice * quantity - discountAmount);

    // Order-level discount and tax stay 0 in this milestone.
    public static OrderTotals Calculate(IEnumerable<PricedLine> lines)
    {
        var subtotal = lines.Sum(l => l.TotalPrice);
        const decimal discount = 0m;
        const decimal tax = 0m;
        return new OrderTotals(subtotal, discount, tax, subtotal - discount + tax);
    }
}
