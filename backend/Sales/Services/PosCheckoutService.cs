using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales.Services;

// Lock order inside the transaction: shift row, then devices (ascending id), then stock rows (product, variant).
// Closing a shift locks only the shift row, so a sale and a close can never wait on each other in a cycle.
public class PosCheckoutService(
    IOrderService orderService,
    IOrderRepository orderRepository,
    IPosSessionRepository sessionRepository,
    IInventoryFulfillmentService fulfillmentService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IPosCheckoutService
{
    private const int MaxCodeAttempts = 5;
    private const string OrderCodeConstraint = "idx_orders_code";
    private const string CheckoutKeyConstraint = "uq_orders_cashier_checkout_key";

    public async Task<PosResult<PosCheckoutOutcome>> CheckoutAsync(
        PosCheckoutRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return PosResult<PosCheckoutOutcome>.ValidationFailed("request", "Request body cannot be null.");
        }

        var errors = new Dictionary<string, string[]>();
        if (!request.CheckoutKey.HasValue || request.CheckoutKey.Value == Guid.Empty)
        {
            errors["checkoutKey"] = ["A checkout key is required."];
        }

        if (!request.PosSessionId.HasValue || request.PosSessionId.Value == Guid.Empty)
        {
            errors["posSessionId"] = ["A shift is required."];
        }

        if (!request.CashTendered.HasValue
            || request.CashTendered.Value < 0
            || decimal.Round(request.CashTendered.Value, 2) != request.CashTendered.Value)
        {
            errors["cashTendered"] = ["The cash received must not be negative and has at most 2 decimal places."];
        }

        if (errors.ContainsKey("checkoutKey") || errors.ContainsKey("posSessionId"))
        {
            return PosResult<PosCheckoutOutcome>.ValidationFailed(errors);
        }

        var key = request.CheckoutKey!.Value;
        var shiftId = request.PosSessionId!.Value;

        // A repeated key returns the stored order, even if its items are no longer available.
        var existingId = await orderRepository.FindIdByCheckoutKeyAsync(actor.Id, key, cancellationToken);
        if (existingId.HasValue)
        {
            return await LoadAsync(existingId.Value, actor, replayed: true, cancellationToken);
        }

        // Prices and totals are server-side. Availability is checked later, under locks.
        var prepared = await orderService.PrepareAsync(
            new CreateOrderRequest
            {
                CustomerId = request.CustomerId,
                PaymentMethod = nameof(PaymentMethod.Cash),
                Notes = request.Notes,
                Items = request.Items
            },
            actor,
            checkAvailability: false,
            cancellationToken);

        if (!prepared.IsSuccess)
        {
            foreach (var (field, messages) in prepared.ValidationErrors!)
            {
                errors[field] = errors.TryGetValue(field, out var own) ? [.. own, .. messages] : messages;
            }
        }
        else if (!errors.ContainsKey("cashTendered") && request.CashTendered!.Value < prepared.Build!(timeProvider.GetUtcNow().UtcDateTime).TotalAmount)
        {
            errors["cashTendered"] = ["The cash received is less than the order total."];
        }

        if (errors.Count > 0)
        {
            return PosResult<PosCheckoutOutcome>.ValidationFailed(errors);
        }

        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var order = prepared.Build!(now);

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var shift = await sessionRepository.GetForUpdateAsync(shiftId, cancellationToken);
                if (shift == null || shift.CashierUserId != actor.Id)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return PosResult<PosCheckoutOutcome>.NotFound();
                }

                if (shift.Status != PosSessionStatus.Open)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return PosResult<PosCheckoutOutcome>.Conflict("The shift is closed. Open a new shift to keep selling.");
                }

                // A request with the same key may have committed while this one waited for the shift lock.
                var committedId = await orderRepository.FindIdByCheckoutKeyAsync(actor.Id, key, cancellationToken);
                if (committedId.HasValue)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return await LoadAsync(committedId.Value, actor, replayed: true, cancellationToken);
                }

                var lastCode = await orderRepository.GetLastOrderCodeAsync(OrderCodeGenerator.GetPrefix(now), cancellationToken);
                order.OrderCode = OrderCodeGenerator.Next(now, lastCode);
                orderRepository.Add(order);

                var items = order.Items
                    .OrderBy(i => i.ProductName, StringComparer.Ordinal)
                    .ThenBy(i => i.Sku, StringComparer.Ordinal)
                    .ThenBy(i => i.Id)
                    .ToList();
                var lines = items
                    .Select(i => new FulfillmentLine(i.Id, i.ProductId, i.VariantId, i.SerialImeiId, i.Quantity))
                    .ToList();

                var reserve = await fulfillmentService.ReserveAsync(lines, cancellationToken);
                if (!reserve.IsSuccess)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return PosResult<PosCheckoutOutcome>.Conflict(OrderService.Describe(items, reserve.Conflict!));
                }

                var sale = await fulfillmentService.ConfirmSaleAsync(lines, now, actor.Id, cancellationToken);
                if (!sale.IsSuccess)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return PosResult<PosCheckoutOutcome>.Conflict(OrderService.Describe(items, sale.Conflict!));
                }

                foreach (var item in items)
                {
                    item.UnitCost = sale.UnitCosts.GetValueOrDefault(item.Id);
                }

                // Every Inventory step succeeded: only now complete the order and credit the shift.
                order.Status = OrderStatus.Completed;
                order.PaymentMethod = PaymentMethod.Cash;
                order.PosSessionId = shift.Id;
                order.CheckoutKey = key;
                order.CashTendered = request.CashTendered!.Value;
                order.ChangeGiven = request.CashTendered.Value - order.TotalAmount;
                order.PaidAt = now;
                order.CompletedAt = now;
                shift.CashSalesTotal += order.TotalAmount;
                shift.UpdatedAt = now;

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == OrderCodeConstraint)
            {
                // Lost the race for this code: roll back (clears the tracker) and try again with a fresh code.
                await unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == CheckoutKeyConstraint)
            {
                // The same key committed through another shift row: return what it stored.
                await unitOfWork.RollbackAsync(cancellationToken);
                var winner = await orderRepository.FindIdByCheckoutKeyAsync(actor.Id, key, cancellationToken);
                return winner.HasValue
                    ? await LoadAsync(winner.Value, actor, replayed: true, cancellationToken)
                    : PosResult<PosCheckoutOutcome>.Conflict("The checkout conflicts with existing data. Please try again.");
            }
            catch (DuplicateKeyException)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<PosCheckoutOutcome>.Conflict(
                    "The order conflicts with existing data (for example a device already used by another order).");
            }
            catch
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            return await LoadAsync(order.Id, actor, replayed: false, cancellationToken);
        }

        return PosResult<PosCheckoutOutcome>.Conflict("Could not allocate a unique order code. Please try again.");
    }

    private async Task<PosResult<PosCheckoutOutcome>> LoadAsync(
        Guid orderId,
        AuthenticatedUser actor,
        bool replayed,
        CancellationToken cancellationToken)
    {
        var detail = await orderService.GetByIdAsync(orderId, actor, cancellationToken);
        return detail.Found
            ? PosResult<PosCheckoutOutcome>.Success(new PosCheckoutOutcome(detail.Data!, replayed))
            : PosResult<PosCheckoutOutcome>.NotFound();
    }
}
