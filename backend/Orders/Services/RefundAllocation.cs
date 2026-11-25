using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Services;

public sealed record RefundLineAllocation(
    Guid OrderItemId, int Quantity, decimal NetAmount, decimal DiscountAmount, decimal TaxAmount)
{
    public decimal RefundAmount => NetAmount + TaxAmount;
}

public sealed record RefundAllocationResult(
    string? Error, IReadOnlyList<RefundLineAllocation> Lines, decimal RemainingNetAmount, bool IsFullyReturned);

/// <summary>Requires every original sale line and every settled return, including pre-M4 receipts.</summary>
public static class RefundAllocation
{
    private const string Review = "The original sale and settled refunds do not reconcile. Review the receipts before paying.";

    public static RefundAllocationResult Calculate(Order order, IReadOnlyDictionary<Guid, int> requested)
    {
        var items = order.Items.ToDictionary(i => i.Id);
        if (items.Count == 0 || order.Returns.Any(r => !items.ContainsKey(r.OrderItemId))
            || order.Subtotal != items.Values.Sum(i => i.TotalPrice)
            || order.DiscountAmount != items.Values.Sum(i => i.OrderDiscountAmount)
            || order.TaxAmount != items.Values.Sum(i => i.TaxAmount)
            || order.TotalAmount != order.Subtotal - order.DiscountAmount + order.TaxAmount)
        {
            return Failure(Review);
        }

        foreach (var (id, quantity) in requested)
        {
            if (!items.ContainsKey(id) || quantity <= 0)
                return Failure($"Invalid return line or quantity for order item {id}.");
        }

        var receipts = order.Returns.ToLookup(r => r.OrderItemId);
        var lines = new List<RefundLineAllocation>(requested.Count);
        var remainingNet = 0m;
        var fullyReturned = true;
        foreach (var item in items.Values)
        {
            var original = new Components(item.TotalPrice - item.OrderDiscountAmount,
                item.DiscountAmount + item.OrderDiscountAmount, item.TaxAmount);
            if (item.Quantity <= 0 || item.UnitPrice < 0m || item.DiscountAmount < 0m || item.OrderDiscountAmount < 0m
                || item.TotalPrice != item.UnitPrice * item.Quantity - item.DiscountAmount || original.HasNegative)
            {
                return Failure(Review);
            }

            if (!TryReadSettled(item.Quantity, original, receipts[item.Id], out var settledQuantity, out var settled))
                return Failure(Review);

            var quantity = requested.GetValueOrDefault(item.Id);
            if (quantity > item.Quantity - settledQuantity)
                return Failure($"Return quantity exceeds the remaining quantity for order item {item.Id}.");

            if (quantity > 0)
            {
                var cumulative = original.Share(settledQuantity + quantity, item.Quantity);
                var delta = cumulative - settled;
                if (delta.HasNegative)
                    return Failure(Review);
                lines.Add(new(item.Id, quantity, delta.Net, delta.Discount, delta.Tax));
                settled = cumulative;
            }
            remainingNet += original.Net - settled.Net;
            fullyReturned &= settledQuantity + quantity == item.Quantity;
        }

        return new(null, lines, remainingNet, fullyReturned);
    }

    private static bool TryReadSettled(int sold, Components original, IEnumerable<OrderReturn> receipts,
        out int quantity, out Components amounts)
    {
        quantity = 0;
        amounts = default;
        var legacyQuantity = 0;
        Components legacy = default;
        foreach (var receipt in receipts.OrderBy(r => r.RefundedAt).ThenBy(r => r.Id))
        {
            if (receipt.Quantity <= 0 || receipt.Quantity > sold - quantity || receipt.RefundAmount < 0m)
                return false;
            quantity += receipt.Quantity;

            if (receipt.NetAmount is null && receipt.DiscountAmount is null && receipt.TaxAmount is null)
            {
                legacyQuantity += receipt.Quantity;
                // Pre-M4 receipts record only the payout. Check each before inferring its components.
                var cumulative = original.Share(legacyQuantity, sold);
                var delta = cumulative - legacy;
                if (delta.Net + delta.Tax != receipt.RefundAmount)
                    return false;
                legacy = cumulative;
                continue;
            }

            if (receipt.NetAmount is not decimal net || receipt.DiscountAmount is not decimal discount
                || receipt.TaxAmount is not decimal tax || receipt.RefundAmount != net + tax)
                return false;
            var recorded = new Components(net, discount, tax);
            if (recorded.HasNegative)
                return false;
            amounts += recorded;
        }

        amounts += legacy;
        return !(original - amounts).HasNegative && (quantity != sold || original == amounts);
    }

    private static RefundAllocationResult Failure(string message) => new(message, [], 0m, false);

    private readonly record struct Components(decimal Net, decimal Discount, decimal Tax)
    {
        public bool HasNegative => Net < 0m || Discount < 0m || Tax < 0m;

        public Components Share(int quantity, int sold)
        {
            decimal Prorate(decimal value) => decimal.Round(value * quantity / sold, 2, MidpointRounding.AwayFromZero);
            return quantity == sold ? this : new(Prorate(Net), Prorate(Discount), Prorate(Tax));
        }

        public static Components operator +(Components a, Components b) =>
            new(a.Net + b.Net, a.Discount + b.Discount, a.Tax + b.Tax);

        public static Components operator -(Components a, Components b) =>
            new(a.Net - b.Net, a.Discount - b.Discount, a.Tax - b.Tax);
    }
}
