using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales.Services;

/// <summary>What happened to one bank notification. <see cref="Result"/> is Applied, Replay, Recorded or Duplicate.</summary>
public record VietQrApplyResult(string Result, VietQrEventReason Reason, VietQrPaymentDto? Payment);

/// <summary>A bank notification that passed signature checks (sandbox webhook).</summary>
public record VietQrBankNotice(string TransactionId, decimal Amount, string TransferContent, string AccountNumber, DateTime TransferredAt, string RawBody);

public interface IVietQrConfirmationService
{
    Task<PosResult<VietQrApplyResult>> ConfirmManualAsync(Guid qrId, ConfirmVietQrRequest request, AuthenticatedUser actor, CancellationToken ct = default);
    Task<PosResult<VietQrApplyResult>> ReceiveSandboxAsync(VietQrBankNotice notice, CancellationToken ct = default);
    Task<PosResult<PagedResultDto<VietQrBankEventDto>>> SearchEventsAsync(VietQrEventSearchRequest request, CancellationToken ct = default);
    Task<PosResult<VietQrBankEventDto>> ResolveEventAsync(Guid id, ResolveVietQrEventRequest request, AuthenticatedUser actor, CancellationToken ct = default);
}

/// <summary>
/// The only place money for a VietQR request is accepted, by an Admin or Manager or by the sandbox webhook. Lock order:
/// shift, order, the order's requests, then (inside the settlement) devices, stock, voucher, customer. Money that cannot
/// complete a sale is stored as a bank event and never changes the order or a closed shift.
/// </summary>
public sealed class VietQrConfirmationService(
    IVietQrHoldRepository payments,
    IPosSessionRepository shifts,
    IOrderRepository orders,
    IVietQrHoldService holds,
    ISaleSettlementService settlement,
    IUnitOfWork work,
    IOptions<VietQrOptions> options,
    TimeProvider clock) : IVietQrConfirmationService
{
    public const string ManualProvider = "manual";
    public const string SandboxProvider = "sandbox";
    private const string EventConstraint = "uq_vietqr_events_provider_transaction";
    private const string ReferenceConstraint = "uq_vietqr_provider_transaction";
    private const int MaxResolutionNote = 500;
    private static readonly Regex TransferCode = new("TS[A-HJ-NP-Z2-9]{10}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record Notice(
        string Provider,
        string Reference,
        decimal Amount,
        string? AccountNumber,
        string TransferContent,
        DateTime TransferredAt,
        string Payload,
        Guid? ConfirmedByUserId);

    // ---------------------------------------------------------------- manual

    public async Task<PosResult<VietQrApplyResult>> ConfirmManualAsync(
        Guid qrId, ConfirmVietQrRequest request, AuthenticatedUser actor, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var reference = request?.ReferenceCode?.Trim();
        if (string.IsNullOrEmpty(reference) || reference.Length > 100)
            errors["referenceCode"] = ["The bank reference is required (at most 100 characters)."];
        if (request?.ReceivedAmount is not > 0m)
            errors["receivedAmount"] = ["The received amount is required and must be above zero."];
        if (errors.Count > 0) return PosResult<VietQrApplyResult>.ValidationFailed(errors);

        var notice = new Notice(ManualProvider, reference!, request!.ReceivedAmount!.Value, null, "manual confirmation",
            clock.GetUtcNow().UtcDateTime,
            JsonSerializer.Serialize(new { referenceCode = reference, receivedAmount = request.ReceivedAmount.Value }), actor.Id);
        return await RunAsync(qrId, null, notice, webhook: false, ct);
    }

    // --------------------------------------------------------------- webhook

    public Task<PosResult<VietQrApplyResult>> ReceiveSandboxAsync(VietQrBankNotice n, CancellationToken ct = default)
    {
        var code = TransferCode.Match(n.TransferContent.ToUpperInvariant());
        var notice = new Notice(SandboxProvider, n.TransactionId, n.Amount, n.AccountNumber, n.TransferContent, n.TransferredAt, n.RawBody, null);
        return RunAsync(null, code.Success ? code.Value : null, notice, webhook: true, ct);
    }

    // ------------------------------------------------------------------ core

    private async Task<PosResult<VietQrApplyResult>> RunAsync(Guid? knownQrId, string? code, Notice notice, bool webhook, CancellationToken ct)
    {
        var expiredOnce = false;
        while (true)
        {
            var qrId = knownQrId;
            if (qrId == null && code != null) qrId = (await payments.FindByTransferCodeAsync(code, ct))?.Id;
            var owner = qrId.HasValue ? await payments.FindOwnerAsync(qrId.Value, ct) : null;
            if (owner == null && !webhook) return PosResult<VietQrApplyResult>.NotFound();

            await work.BeginTransactionAsync(ct);
            try
            {
                var shift = owner?.ShiftId is Guid shiftId ? await shifts.GetForUpdateAsync(shiftId, ct) : null;
                var order = owner != null ? await orders.GetForUpdateWithItemsAsync(owner.OrderId, ct) : null;
                var attempts = order != null ? await payments.LockPaymentsAsync(order.Id, ct) : [];
                var qr = qrId.HasValue ? attempts.SingleOrDefault(q => q.Id == qrId.Value) : null;
                var now = clock.GetUtcNow().UtcDateTime;

                var prior = await payments.FindEventAsync(notice.Provider, notice.Reference, ct);
                if (prior != null)
                {
                    await work.RollbackAsync(CancellationToken.None);
                    return await DuplicateAsync(prior, ct);
                }

                if (qr == null || order == null)
                    return await RecordAsync(notice, null, VietQrEventReason.Unmatched, webhook, now,
                        "Không tìm thấy yêu cầu VietQR cho giao dịch này.", ct);

                if (qr.Status == VietQrStatus.Confirmed)
                {
                    await work.RollbackAsync(CancellationToken.None);
                    if (qr.Provider == notice.Provider && qr.ProviderTransactionId == notice.Reference)
                        return await ReplayAsync(qr.Id, ct);
                    return await RecordAfterRollbackAsync(notice, qr.Id, VietQrEventReason.DuplicateReference, webhook,
                        "Đơn này đã được thanh toán bằng một giao dịch khác.", ct);
                }

                if (await payments.ReferenceUsedAsync(notice.Provider, notice.Reference, qr.Id, ct))
                {
                    await work.RollbackAsync(CancellationToken.None);
                    return await RecordAfterRollbackAsync(notice, qr.Id, VietQrEventReason.DuplicateReference, webhook,
                        "Mã tham chiếu này đã được dùng cho một giao dịch khác.", ct);
                }

                if (qr.Status == VietQrStatus.Pending && now >= qr.ExpiresAt && !expiredOnce)
                {
                    // Close the overdue request first so the verdict below is made on a settled state.
                    await work.RollbackAsync(CancellationToken.None);
                    await holds.ExpireDueAsync(qr.Id, ct);
                    expiredOnce = true;
                    continue;
                }

                if (qr.Status == VietQrStatus.Expired || qr.Status == VietQrStatus.Pending && now >= qr.ExpiresAt)
                    return await RecordAsync(notice, qr.Id, VietQrEventReason.LateAfterExpiry, webhook, now,
                        "Mã VietQR đã hết hạn. Khoản tiền được lưu để đối soát.", ct);
                if (qr.Status == VietQrStatus.Failed)
                    return await RecordAsync(notice, qr.Id, VietQrEventReason.LateAfterCancel, webhook, now,
                        "Mã VietQR đã bị hủy. Khoản tiền được lưu để đối soát.", ct);
                if (shift == null || shift.Status != PosSessionStatus.Open)
                    return await RecordAsync(notice, qr.Id, VietQrEventReason.LateAfterShiftClose, webhook, now,
                        "Ca đã đóng. Khoản tiền được lưu để đối soát.", ct);

                if (order.PaymentMethod != PaymentMethod.VietQr || order.Status != OrderStatus.Pending || order.PaidAt != null
                    || order.PosSessionId != shift.Id || qr.PosSessionId != shift.Id || qr.Amount != order.TotalAmount)
                {
                    await work.RollbackAsync(CancellationToken.None);
                    return PosResult<VietQrApplyResult>.Conflict("Đơn hoặc giao dịch VietQR không còn chờ thanh toán.");
                }

                if (webhook && !string.Equals(notice.AccountNumber, qr.BankAccountNumber, StringComparison.Ordinal))
                    return await RecordAsync(notice, qr.Id, VietQrEventReason.AccountMismatch, webhook, now,
                        "Tài khoản nhận tiền không khớp.", ct);

                if (notice.Amount != qr.Amount)
                {
                    if (!webhook)
                    {
                        await work.RollbackAsync(CancellationToken.None);
                        return PosResult<VietQrApplyResult>.ValidationFailed("receivedAmount", "The received amount does not match the QR amount.");
                    }

                    return await RecordAsync(notice, qr.Id, VietQrEventReason.AmountMismatch, webhook, now,
                        "Số tiền không khớp với mã VietQR.", ct);
                }

                var settled = await settlement.CompleteAsync(order, shift, PaymentMethod.VietQr, now, notice.ConfirmedByUserId ?? order.CashierUserId, ct);
                if (!settled.IsSuccess)
                {
                    await work.RollbackAsync(CancellationToken.None);
                    return await RecordAfterRollbackAsync(notice, qr.Id, VietQrEventReason.SettlementFailed, webhook, settled.Conflict!, ct);
                }

                qr.Provider = notice.Provider;
                qr.ProviderTransactionId = notice.Reference;
                qr.Status = VietQrStatus.Confirmed;
                qr.ConfirmedAt = now;
                qr.ConfirmedByUserId = notice.ConfirmedByUserId;
                qr.UpdatedAt = now;

                if (webhook) payments.Add(NewEvent(notice, qr.Id, VietQrEventStatus.Applied, VietQrEventReason.Applied, now));
                await work.SaveChangesAsync(ct);
                await work.CommitAsync(ct);

                var paid = await payments.GetByIdAsync(qr.Id, ct);
                return PosResult<VietQrApplyResult>.Success(new("Applied", VietQrEventReason.Applied,
                    paid == null ? null : VietQrMapper.ToDto(paid, now, includeImage: false)));
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName is EventConstraint or ReferenceConstraint)
            {
                // The same notification won the race: whatever it stored is the answer.
                await work.RollbackAsync(CancellationToken.None);
                var stored = await payments.FindEventAsync(notice.Provider, notice.Reference, ct);
                if (stored != null) return await DuplicateAsync(stored, ct);
                return PosResult<VietQrApplyResult>.Conflict("Mã tham chiếu này đã được dùng cho một giao dịch khác.");
            }
            catch
            {
                await work.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    // Stores the money as an event in the open transaction and commits. A manual confirmation that cannot complete the
    // sale still reports the refusal, because the person is checking a bank statement and needs to know.
    private async Task<PosResult<VietQrApplyResult>> RecordAsync(
        Notice notice, Guid? qrId, VietQrEventReason reason, bool webhook, DateTime now, string message, CancellationToken ct)
    {
        payments.Add(NewEvent(notice, qrId, VietQrEventStatus.NeedsReconciliation, reason, now));
        await work.SaveChangesAsync(ct);
        await work.CommitAsync(ct);
        return webhook
            ? PosResult<VietQrApplyResult>.Success(new("Recorded", reason, null))
            : PosResult<VietQrApplyResult>.Conflict(message);
    }

    // Same, for a verdict reached after the transaction was rolled back.
    private async Task<PosResult<VietQrApplyResult>> RecordAfterRollbackAsync(
        Notice notice, Guid? qrId, VietQrEventReason reason, bool webhook, string message, CancellationToken ct)
    {
        // A person confirming by hand just gets the refusal: nothing was received by the system to keep.
        if (!webhook) return PosResult<VietQrApplyResult>.Conflict(message);

        await work.BeginTransactionAsync(ct);
        try
        {
            return await RecordAsync(notice, qrId, reason, webhook, clock.GetUtcNow().UtcDateTime, message, ct);
        }
        catch (DuplicateKeyException ex) when (ex.ConstraintName == EventConstraint)
        {
            await work.RollbackAsync(CancellationToken.None);
            var stored = await payments.FindEventAsync(notice.Provider, notice.Reference, ct);
            return stored != null ? await DuplicateAsync(stored, ct) : PosResult<VietQrApplyResult>.Conflict(message);
        }
        catch
        {
            await work.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<PosResult<VietQrApplyResult>> ReplayAsync(Guid qrId, CancellationToken ct)
    {
        var paid = await payments.GetByIdAsync(qrId, ct);
        return PosResult<VietQrApplyResult>.Success(new("Replay", VietQrEventReason.Applied,
            paid == null ? null : VietQrMapper.ToDto(paid, clock.GetUtcNow().UtcDateTime, includeImage: false)));
    }

    private async Task<PosResult<VietQrApplyResult>> DuplicateAsync(VietQrBankEvent prior, CancellationToken ct)
    {
        VietQrPaymentDto? payment = null;
        if (prior.Status == VietQrEventStatus.Applied && prior.VietQrTransactionId.HasValue)
        {
            var qr = await payments.GetByIdAsync(prior.VietQrTransactionId.Value, ct);
            if (qr != null) payment = VietQrMapper.ToDto(qr, clock.GetUtcNow().UtcDateTime, includeImage: false);
        }

        return PosResult<VietQrApplyResult>.Success(new("Duplicate", prior.Reason, payment));
    }

    private static VietQrBankEvent NewEvent(Notice n, Guid? qrId, VietQrEventStatus status, VietQrEventReason reason, DateTime now) => new()
    {
        Provider = n.Provider,
        ProviderTransactionId = n.Reference,
        Amount = n.Amount,
        TransferContent = n.TransferContent.Length > 200 ? n.TransferContent[..200] : n.TransferContent,
        AccountNumber = n.AccountNumber ?? string.Empty,
        TransferredAt = n.TransferredAt,
        ReceivedAt = now,
        VietQrTransactionId = qrId,
        Status = status,
        Reason = reason,
        Payload = n.Payload,
        CreatedAt = now
    };

    // ---------------------------------------------------------------- events

    public async Task<PosResult<PagedResultDto<VietQrBankEventDto>>> SearchEventsAsync(VietQrEventSearchRequest request, CancellationToken ct = default)
    {
        VietQrEventStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!OrderEnumParser.TryParse<VietQrEventStatus>(request.Status, out var parsed))
                return PosResult<PagedResultDto<VietQrBankEventDto>>.ValidationFailed(
                    "status", $"Status must be one of: {string.Join(", ", Enum.GetNames<VietQrEventStatus>())}.");
            status = parsed;
        }

        var (page, pageSize) = ProductPagingHelper.Normalize(request.Page, request.PageSize);
        var (items, total) = await payments.SearchEventsAsync(new VietQrEventSearchCriteria(status, page, pageSize), ct);
        return PosResult<PagedResultDto<VietQrBankEventDto>>.Success(new PagedResultDto<VietQrBankEventDto>(
            items.Select(Map).ToList(), page, pageSize, total, ProductPagingHelper.CalculateTotalPages(total, pageSize)));
    }

    public async Task<PosResult<VietQrBankEventDto>> ResolveEventAsync(Guid id, ResolveVietQrEventRequest request, AuthenticatedUser actor, CancellationToken ct = default)
    {
        var note = request?.Note?.Trim();
        if (string.IsNullOrEmpty(note))
            return PosResult<VietQrBankEventDto>.ValidationFailed("note", "A resolution note is required.");
        if (note.Length > MaxResolutionNote)
            return PosResult<VietQrBankEventDto>.ValidationFailed("note", $"The note must be at most {MaxResolutionNote} characters.");

        await work.BeginTransactionAsync(ct);
        try
        {
            var bankEvent = await payments.GetEventForUpdateAsync(id, ct);
            if (bankEvent == null)
            {
                await work.RollbackAsync(CancellationToken.None);
                return PosResult<VietQrBankEventDto>.NotFound();
            }

            if (bankEvent.Status == VietQrEventStatus.Applied)
            {
                await work.RollbackAsync(CancellationToken.None);
                return PosResult<VietQrBankEventDto>.Conflict("Khoản tiền này đã hoàn tất một đơn, không cần đối soát.");
            }

            if (bankEvent.Status == VietQrEventStatus.NeedsReconciliation)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                bankEvent.Status = VietQrEventStatus.Resolved;
                bankEvent.ResolvedAt = now;
                bankEvent.ResolvedByUserId = actor.Id;
                bankEvent.ResolutionNote = note;
                bankEvent.UpdatedAt = now;
                await work.SaveChangesAsync(ct);
            }

            await work.CommitAsync(ct);
            return PosResult<VietQrBankEventDto>.Success(Map(bankEvent));
        }
        catch
        {
            await work.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static VietQrBankEventDto Map(VietQrBankEvent e) => new()
    {
        Id = e.Id,
        Provider = e.Provider,
        ProviderTransactionId = e.ProviderTransactionId,
        Amount = e.Amount,
        TransferContent = e.TransferContent,
        AccountNumber = e.AccountNumber,
        TransferredAt = e.TransferredAt,
        ReceivedAt = e.ReceivedAt,
        VietQrTransactionId = e.VietQrTransactionId,
        OrderCode = e.VietQrTransaction?.Order?.OrderCode,
        Status = e.Status.ToString(),
        Reason = e.Reason.ToString(),
        ResolvedByUserId = e.ResolvedByUserId,
        ResolvedAt = e.ResolvedAt,
        ResolutionNote = e.ResolutionNote
    };
}
