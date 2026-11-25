using TechStore.Api.Customers.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Services;

public record SettlementResult(bool IsSuccess, string? Conflict)
{
    public static SettlementResult Success() => new(true, null);
    public static SettlementResult Failed(string conflict) => new(false, conflict);
}

/// <summary>
/// The one place that turns a paid order into a completed sale, for Cash and for VietQR. It only stages changes in the
/// caller's unit of work: the caller opens the transaction, holds the shift and order locks, saves and commits once.
/// </summary>
public interface ISaleSettlementService
{
    /// <summary>
    /// Requires the devices, stock and the voucher use (when the order has a voucher) to be held and saved already.
    /// Running it again for a <c>Completed</c> order changes nothing.
    /// </summary>
    Task<SettlementResult> CompleteAsync(
        Order order,
        PosSession shift,
        PaymentMethod method,
        DateTime paidAt,
        Guid actorId,
        CancellationToken cancellationToken = default);
}

public class SaleSettlementService(
    IInventoryFulfillmentService fulfillmentService,
    IVoucherService voucherService,
    IRefundLoyaltyService? loyalty = null) : ISaleSettlementService
{
    public async Task<SettlementResult> CompleteAsync(
        Order order,
        PosSession shift,
        PaymentMethod method,
        DateTime paidAt,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(shift);
        if (order.Status == OrderStatus.Completed)
        {
            return SettlementResult.Success();
        }

        var items = order.Items
            .OrderBy(i => i.ProductName, StringComparer.Ordinal)
            .ThenBy(i => i.Sku, StringComparer.Ordinal)
            .ThenBy(i => i.Id)
            .ToList();
        var lines = items
            .Select(i => new FulfillmentLine(i.Id, i.ProductId, i.VariantId, i.SerialImeiId, i.Quantity))
            .ToList();

        var sale = await fulfillmentService.ConfirmSaleAsync(lines, paidAt, actorId, cancellationToken);
        if (!sale.IsSuccess)
        {
            return SettlementResult.Failed(OrderService.Describe(items, sale.Conflict!));
        }

        foreach (var item in items)
        {
            item.UnitCost = sale.UnitCosts.GetValueOrDefault(item.Id);
            item.UpdatedAt = paidAt;
        }

        if (order.VoucherId.HasValue)
        {
            var voucher = await voucherService.RedeemAsync(order.Id, cancellationToken);
            if (voucher.Outcome is VoucherUseOutcome.Refused or VoucherUseOutcome.NotFound)
            {
                return SettlementResult.Failed(voucher.Reason ?? "The voucher use could not be consumed.");
            }
        }

        if (loyalty != null && order.CustomerId is Guid customerId)
        {
            await loyalty.StageSaleAsync(order.Id, customerId, order.Subtotal - order.DiscountAmount, cancellationToken);
        }

        order.Status = OrderStatus.Completed;
        order.PaymentMethod = method;
        order.PaidAt = paidAt;
        order.CompletedAt = paidAt;
        order.UpdatedAt = paidAt;

        switch (method)
        {
            case PaymentMethod.Cash:
                shift.CashSalesTotal += order.TotalAmount;
                break;
            case PaymentMethod.VietQr:
                shift.VietQrSalesTotal += order.TotalAmount;
                break;
            default:
                shift.CardSalesTotal += order.TotalAmount;
                break;
        }

        shift.UpdatedAt = paidAt;
        return SettlementResult.Success();
    }
}
