using Microsoft.Extensions.Options;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Services;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales.Services;

// Replayed is true when the checkout key already had a request: it is returned and nothing is held again.
public record VietQrOutcome(VietQrPaymentDto Payment, bool Replayed);

public interface IVietQrPaymentService
{
    Task<PosResult<VietQrOutcome>> CreateAsync(CreateVietQrRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
    Task<PosResult<VietQrPaymentDto>> GetAsync(Guid id, AuthenticatedUser actor, CancellationToken cancellationToken = default);
    Task<PosResult<PagedResultDto<VietQrPaymentDto>>> SearchAsync(VietQrSearchRequest request, AuthenticatedUser actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates a VietQR payment request: the order, the hold on devices, stock and voucher, and the QR itself, in one
/// transaction. Lock order: shift, then the new order's devices and stock, then the voucher.
/// </summary>
public sealed class VietQrPaymentService(
    IOrderService orderService,
    IOrderRepository orderRepository,
    IPosSessionRepository sessionRepository,
    IVietQrHoldRepository payments,
    IVietQrHoldService holds,
    IInventoryFulfillmentService fulfillmentService,
    IVoucherService vouchers,
    IUnitOfWork unitOfWork,
    IOptions<VietQrOptions> options,
    TimeProvider timeProvider) : IVietQrPaymentService
{
    private const int MaxAttempts = 5;
    private const string OrderCodeConstraint = "idx_orders_code";
    private const string TransferCodeConstraint = "idx_vietqr_tx_code";
    private const string CheckoutKeyConstraint = "uq_orders_cashier_checkout_key";
    private const string PendingOrderConstraint = "uq_vietqr_pending_order";

    public async Task<PosResult<VietQrOutcome>> CreateAsync(
        CreateVietQrRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return PosResult<VietQrOutcome>.ValidationFailed("request", "Request body cannot be null.");
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

        if (errors.Count > 0)
        {
            return PosResult<VietQrOutcome>.ValidationFailed(errors);
        }

        var bank = options.Value;
        if (!BankIsUsable(bank))
        {
            return PosResult<VietQrOutcome>.Unavailable("VietQR chưa được cấu hình tài khoản nhận tiền.");
        }

        var key = request.CheckoutKey!.Value;
        var shiftId = request.PosSessionId!.Value;

        // A repeated key returns the stored request, even if its items are no longer available.
        var existing = await FindByKeyAsync(actor.Id, key, cancellationToken);
        if (existing.Conflict != null) return PosResult<VietQrOutcome>.Conflict(existing.Conflict);
        if (existing.Payment != null) return PosResult<VietQrOutcome>.Success(new(existing.Payment, true));

        var prepared = await orderService.PrepareAsync(
            new CreateOrderRequest
            {
                PaymentMethod = nameof(PaymentMethod.VietQr),
                CustomerId = request.CustomerId,
                OrderDiscountAmount = request.OrderDiscountAmount,
                VoucherCode = request.VoucherCode,
                ApplyVipDiscount = request.ApplyVipDiscount,
                Notes = request.Notes,
                Items = request.Items
            },
            actor,
            checkAvailability: false,
            cancellationToken);
        if (!prepared.IsSuccess)
        {
            return PosResult<VietQrOutcome>.ValidationFailed(prepared.ValidationErrors!);
        }

        var total = prepared.Build!(timeProvider.GetUtcNow().UtcDateTime).TotalAmount;
        if (request.ExpectedTotal is decimal expected && expected != total)
        {
            return PosResult<VietQrOutcome>.Conflict("Giá đã thay đổi. Vui lòng kiểm tra lại tổng tiền.");
        }

        if (total <= 0m)
        {
            return PosResult<VietQrOutcome>.ValidationFailed("items", "A QR payment needs a total above zero.");
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var order = prepared.Build!(now);
            var transferCode = VietQrPayload.NewTransferCode();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            Guid qrId;
            try
            {
                var shift = await sessionRepository.GetForUpdateAsync(shiftId, cancellationToken);
                if (shift == null || shift.CashierUserId != actor.Id)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return PosResult<VietQrOutcome>.NotFound();
                }

                if (shift.Status != PosSessionStatus.Open)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return PosResult<VietQrOutcome>.Conflict("The shift is closed. Open a new shift to keep selling.");
                }

                // A request with the same key may have committed while this one waited for the shift lock.
                var committed = await FindByKeyAsync(actor.Id, key, cancellationToken);
                if (committed.Payment != null || committed.Conflict != null)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return committed.Conflict != null
                        ? PosResult<VietQrOutcome>.Conflict(committed.Conflict)
                        : PosResult<VietQrOutcome>.Success(new(committed.Payment!, true));
                }

                var lastCode = await orderRepository.GetLastOrderCodeAsync(OrderCodeGenerator.GetPrefix(now), cancellationToken);
                order.OrderCode = OrderCodeGenerator.Next(now, lastCode);
                order.PosSessionId = shift.Id;
                order.CheckoutKey = key;
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
                    return PosResult<VietQrOutcome>.Conflict(OrderService.Describe(items, reserve.Conflict!));
                }

                if (order.VoucherId.HasValue)
                {
                    var hold = await vouchers.ReserveAsync(order, cancellationToken);
                    if (hold.Outcome is VoucherUseOutcome.Refused or VoucherUseOutcome.NotFound)
                    {
                        await unitOfWork.RollbackAsync(cancellationToken);
                        return PosResult<VietQrOutcome>.Conflict(hold.Reason ?? "The voucher cannot be used for this order.");
                    }
                }

                var qr = new VietQrTransaction
                {
                    OrderId = order.Id,
                    PosSessionId = shift.Id,
                    TransactionCode = transferCode,
                    BankBin = bank.BankBin,
                    BankAccountNumber = bank.AccountNumber,
                    BankAccountName = bank.AccountName,
                    Amount = order.TotalAmount,
                    QrContent = VietQrPayload.Build(bank.BankBin, bank.AccountNumber, order.TotalAmount, transferCode),
                    Status = VietQrStatus.Pending,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(bank.HoldMinutes),
                };
                payments.Add(qr);
                qrId = qr.Id;

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName is OrderCodeConstraint or TransferCodeConstraint)
            {
                // Lost the race for a code: roll back (clears the tracker) and try again with fresh codes.
                await unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == CheckoutKeyConstraint)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                var winner = await FindByKeyAsync(actor.Id, key, cancellationToken);
                return winner.Payment != null
                    ? PosResult<VietQrOutcome>.Success(new(winner.Payment, true))
                    : PosResult<VietQrOutcome>.Conflict("The payment request conflicts with existing data. Please try again.");
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == PendingOrderConstraint)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<VietQrOutcome>.Conflict("This order already has a payment request waiting.");
            }
            catch (DuplicateKeyException)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<VietQrOutcome>.Conflict(
                    "The order conflicts with existing data (for example a device already used by another order).");
            }
            catch
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            var saved = await payments.GetByIdAsync(qrId, cancellationToken);
            return saved == null
                ? PosResult<VietQrOutcome>.NotFound()
                : PosResult<VietQrOutcome>.Success(new(VietQrMapper.ToDto(saved, now, includeImage: true), false));
        }

        return PosResult<VietQrOutcome>.Conflict("Could not allocate a unique code. Please try again.");
    }

    public async Task<PosResult<VietQrPaymentDto>> GetAsync(Guid id, AuthenticatedUser actor, CancellationToken cancellationToken = default)
    {
        var qr = await payments.GetByIdAsync(id, cancellationToken);
        if (qr == null || !CanAccess(actor, qr)) return PosResult<VietQrPaymentDto>.NotFound();

        // The deadline is enforced on read too, so a closed or idle POS page never shows a request that can no longer be paid.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (qr.Status == VietQrStatus.Pending && now >= qr.ExpiresAt)
        {
            await holds.ExpireDueAsync(id, cancellationToken);
            qr = await payments.GetByIdAsync(id, cancellationToken);
            if (qr == null) return PosResult<VietQrPaymentDto>.NotFound();
        }

        return PosResult<VietQrPaymentDto>.Success(VietQrMapper.ToDto(qr, now, includeImage: true));
    }

    public async Task<PosResult<PagedResultDto<VietQrPaymentDto>>> SearchAsync(
        VietQrSearchRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        VietQrStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!OrderEnumParser.TryParse<VietQrStatus>(request.Status, out var parsed))
            {
                return PosResult<PagedResultDto<VietQrPaymentDto>>.ValidationFailed(
                    "status", $"Status must be one of: {string.Join(", ", Enum.GetNames<VietQrStatus>())}.");
            }

            status = parsed;
        }

        var cashier = actor.Role == RoleType.SalesStaff ? actor.Id : (Guid?)null;
        var (page, pageSize) = ProductPagingHelper.Normalize(request.Page, request.PageSize);
        var (items, total) = await payments.SearchAsync(
            new VietQrSearchCriteria(status, request.PosSessionId, cashier, page, pageSize), cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return PosResult<PagedResultDto<VietQrPaymentDto>>.Success(new PagedResultDto<VietQrPaymentDto>(
            items.Select(q => VietQrMapper.ToDto(q, now, includeImage: false)).ToList(),
            page,
            pageSize,
            total,
            ProductPagingHelper.CalculateTotalPages(total, pageSize)));
    }

    // SalesStaff reach only their own requests; Manager and Admin reach every one.
    internal static bool CanAccess(AuthenticatedUser actor, VietQrTransaction qr) =>
        actor.Role != RoleType.SalesStaff || qr.Order.CashierUserId == actor.Id;

    private static bool BankIsUsable(VietQrOptions bank)
    {
        if (!bank.IsBankConfigured || bank.HoldMinutes < 1) return false;
        try
        {
            VietQrPayload.Build(bank.BankBin, bank.AccountNumber, 1m, "TSAAAAAAAAAA");
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task<(VietQrPaymentDto? Payment, string? Conflict)> FindByKeyAsync(Guid cashierId, Guid key, CancellationToken cancellationToken)
    {
        var orderId = await orderRepository.FindIdByCheckoutKeyAsync(cashierId, key, cancellationToken);
        if (!orderId.HasValue) return (null, null);

        var qr = await payments.GetLatestByOrderAsync(orderId.Value, cancellationToken);
        return qr == null
            ? (null, "This checkout key was already used for another payment.")
            : (VietQrMapper.ToDto(qr, timeProvider.GetUtcNow().UtcDateTime, includeImage: true), null);
    }
}

internal static class VietQrMapper
{
    public static VietQrPaymentDto ToDto(VietQrTransaction qr, DateTime nowUtc, bool includeImage) => new()
    {
        Id = qr.Id,
        OrderId = qr.OrderId,
        OrderCode = qr.Order.OrderCode,
        Status = qr.Status.ToString(),
        OrderStatus = qr.Order.Status.ToString(),
        Amount = qr.Amount,
        TransactionCode = qr.TransactionCode,
        BankBin = qr.BankBin,
        BankAccountNumber = qr.BankAccountNumber,
        BankAccountName = qr.BankAccountName,
        QrContent = qr.QrContent,
        QrImagePng = includeImage ? VietQrPayload.ToPngBase64(qr.QrContent) : null,
        QrModules = includeImage ? VietQrPayload.ToModules(qr.QrContent) : null,
        CreatedAt = qr.CreatedAt,
        ExpiresAt = qr.ExpiresAt,
        SecondsLeft = qr.Status == VietQrStatus.Pending ? Math.Max(0, (int)Math.Ceiling((qr.ExpiresAt - nowUtc).TotalSeconds)) : 0,
        PosSessionId = qr.PosSessionId,
        CashierUserId = qr.Order.CashierUserId,
        CashierName = qr.Order.CashierUser?.FullName,
        ConfirmedAt = qr.ConfirmedAt
    };
}
