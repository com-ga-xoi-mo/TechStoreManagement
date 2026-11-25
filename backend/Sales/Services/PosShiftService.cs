using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales.Services;

public class PosShiftService(
    IPosSessionRepository sessionRepository,
    IVietQrHoldRepository vietQr,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IPosShiftService
{
    public const int MaxNoteLength = 500;
    private const string OpenShiftConstraint = "uq_pos_sessions_cashier_open";
    private const string AlreadyOpen = "You already have an open shift. Close it before opening a new one.";

    public async Task<PosResult<PosShiftDto>> OpenAsync(
        OpenShiftRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateMoney(request?.OpeningBalance, out var opening, out var message))
        {
            return PosResult<PosShiftDto>.ValidationFailed("openingBalance", message);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var shift = new PosSession
        {
            CashierUserId = actor.Id,
            OpeningBalance = opening,
            OpenedAt = now,
            CreatedAt = now
        };

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await sessionRepository.GetOpenByCashierAsync(actor.Id, cancellationToken) != null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<PosShiftDto>.Conflict(AlreadyOpen);
            }

            sessionRepository.Add(shift);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DuplicateKeyException ex) when (ex.ConstraintName == OpenShiftConstraint)
        {
            // A simultaneous request opened the shift first.
            await unitOfWork.RollbackAsync(cancellationToken);
            return PosResult<PosShiftDto>.Conflict(AlreadyOpen);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var saved = await sessionRepository.GetByIdAsync(shift.Id, cancellationToken) ?? shift;
        return PosResult<PosShiftDto>.Success(MapToDto(saved));
    }

    public async Task<PosResult<PosShiftDto>> GetCurrentAsync(
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        var shift = await sessionRepository.GetOpenByCashierAsync(actor.Id, cancellationToken);
        return shift == null ? PosResult<PosShiftDto>.NotFound() : PosResult<PosShiftDto>.Success(MapToDto(shift));
    }

    public async Task<PosResult<PosShiftDto>> GetByIdAsync(
        Guid id,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        var shift = await sessionRepository.GetByIdAsync(id, cancellationToken);
        return shift == null || !CanAccess(actor, shift)
            ? PosResult<PosShiftDto>.NotFound()
            : PosResult<PosShiftDto>.Success(MapToDto(shift));
    }

    public async Task<PosResult<PagedResultDto<PosShiftDto>>> SearchAsync(
        PosShiftSearchRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        PosSessionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (OrderEnumParser.TryParse<PosSessionStatus>(request.Status, out var parsed))
            {
                status = parsed;
            }
            else
            {
                errors["status"] = [$"Status must be one of: {string.Join(", ", Enum.GetNames<PosSessionStatus>())}."];
            }
        }

        if (request.CashierUserId.HasValue && request.CashierUserId.Value == Guid.Empty)
        {
            errors["cashierUserId"] = ["Cashier ID is invalid."];
        }

        if (errors.Count > 0)
        {
            return PosResult<PagedResultDto<PosShiftDto>>.ValidationFailed(errors);
        }

        // SalesStaff only ever see their own shifts, whatever filter they send.
        var cashier = actor.Role == RoleType.SalesStaff ? actor.Id : request.CashierUserId;
        var (page, pageSize) = ProductPagingHelper.Normalize(request.Page, request.PageSize);
        var (items, total) = await sessionRepository.SearchAsync(
            new PosSessionSearchCriteria(status, cashier, page, pageSize), cancellationToken);

        return PosResult<PagedResultDto<PosShiftDto>>.Success(new PagedResultDto<PosShiftDto>(
            items.Select(MapToDto).ToList(),
            page,
            pageSize,
            total,
            ProductPagingHelper.CalculateTotalPages(total, pageSize)));
    }

    public async Task<PosResult<PosShiftDto>> CloseAsync(
        Guid id,
        CloseShiftRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (!TryValidateMoney(request?.ClosingBalance, out var counted, out var message))
        {
            errors["closingBalance"] = [message];
        }

        var note = string.IsNullOrWhiteSpace(request?.Note) ? null : request!.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            errors["note"] = [$"Note must be at most {MaxNoteLength} characters."];
        }

        if (errors.Count > 0)
        {
            return PosResult<PosShiftDto>.ValidationFailed(errors);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var shift = await sessionRepository.GetForUpdateAsync(id, cancellationToken);
            if (shift == null || !CanAccess(actor, shift))
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<PosShiftDto>.NotFound();
            }

            if (shift.Status != PosSessionStatus.Open)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<PosShiftDto>.Conflict("The shift is already closed.");
            }

            // Creating, confirming and expiring a QR all lock the shift first, so this answer cannot change before the close.
            var waiting = await vietQr.ListPendingByShiftAsync(shift.Id, cancellationToken);
            if (waiting.Count > 0)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                var codes = string.Join(", ", waiting.Select(q => q.Order.OrderCode));
                return PosResult<PosShiftDto>.Conflict(
                    $"Ca còn {waiting.Count} mã VietQR đang chờ thanh toán ({codes}). Hãy hủy hoặc chờ xác nhận trước khi đóng ca.");
            }

            var expected = shift.OpeningBalance + shift.CashSalesTotal - shift.CashRefundTotal;
            if (counted != expected && note == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return PosResult<PosShiftDto>.ValidationFailed(
                    "note", "A note is required when the counted cash differs from the expected cash.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            shift.ExpectedClosingBalance = expected;
            shift.ClosingBalance = counted;
            shift.ClosingNote = note;
            shift.ClosedByUserId = actor.Id;
            shift.ClosedAt = now;
            shift.Status = PosSessionStatus.Closed;
            shift.UpdatedAt = now;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var closed = await sessionRepository.GetByIdAsync(id, cancellationToken);
        return closed == null ? PosResult<PosShiftDto>.NotFound() : PosResult<PosShiftDto>.Success(MapToDto(closed));
    }

    // SalesStaff reach only their own shift; Manager and Admin reach every shift.
    internal static bool CanAccess(AuthenticatedUser actor, PosSession shift) =>
        actor.Role != RoleType.SalesStaff || shift.CashierUserId == actor.Id;

    private static bool TryValidateMoney(decimal? value, out decimal amount, out string message)
    {
        amount = 0;
        message = string.Empty;
        if (!value.HasValue)
        {
            message = "A value is required.";
            return false;
        }

        if (value.Value < 0 || decimal.Round(value.Value, 2) != value.Value)
        {
            message = "The amount must not be negative and has at most 2 decimal places.";
            return false;
        }

        amount = value.Value;
        return true;
    }

    internal static PosShiftDto MapToDto(PosSession shift) => new()
    {
        Id = shift.Id,
        CashierUserId = shift.CashierUserId,
        CashierName = shift.CashierUser?.FullName,
        Status = shift.Status.ToString(),
        OpeningBalance = shift.OpeningBalance,
        CashSalesTotal = shift.CashSalesTotal,
        CashRefundTotal = shift.CashRefundTotal,
        VietQrSalesTotal = shift.VietQrSalesTotal,
        ExpectedClosingBalance = shift.ExpectedClosingBalance
            ?? (shift.Status == PosSessionStatus.Open ? shift.OpeningBalance + shift.CashSalesTotal - shift.CashRefundTotal : null),
        ClosingBalance = shift.ClosingBalance,
        Discrepancy = shift.ClosingBalance - shift.ExpectedClosingBalance,
        ClosingNote = shift.ClosingNote,
        ClosedByUserId = shift.ClosedByUserId,
        OpenedAt = shift.OpenedAt,
        ClosedAt = shift.ClosedAt
    };
}
