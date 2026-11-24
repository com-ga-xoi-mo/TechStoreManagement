using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Services;

public sealed class CashRefundService(IPosSessionRepository sessions, ICashRefundRepository payouts) : ICashRefundService
{
    public async Task<Guid?> LockOpenShiftAsync(Guid actorId, CancellationToken ct)
    {
        var current = await sessions.GetOpenByCashierAsync(actorId, ct);
        if (current == null) return null;
        var locked = await sessions.GetForUpdateAsync(current.Id, ct);
        return locked?.Status == PosSessionStatus.Open && locked.CashierUserId == actorId ? locked.Id : null;
    }

    public async Task StagePayoutAsync(Guid refundId, Guid shiftId, Guid actorId, decimal amount, CancellationToken ct)
    {
        var shift = await sessions.GetForUpdateAsync(shiftId, ct);
        if (shift?.Status != PosSessionStatus.Open || shift.CashierUserId != actorId || amount < 0m)
            throw new InvalidOperationException("A cash refund requires the actor's locked, open shift and a valid amount.");
        var previous = await payouts.FindAsync(refundId, ct);
        if (previous != null)
        {
            if (previous.ShiftId != shiftId || previous.Amount != amount)
                throw new InvalidOperationException("The recorded payout differs from this refund.");
            return;
        }
        if (amount > shift.OpeningBalance + shift.CashSalesTotal - shift.CashRefundTotal)
            throw new CashRefundConflictException("Số tiền trong ca không đủ để chi khoản hoàn.");
        payouts.Add(new CashRefundPayout { RefundId = refundId, ShiftId = shiftId, Amount = amount });
        shift.CashRefundTotal += amount;
    }
}
