namespace TechStore.Api.Sales.Services;

// Both calls run inside the refund coordinator's transaction.
// Lock the actor's own open shift before Orders locks the order. Stage once per refund ID.
public sealed class CashRefundConflictException(string message) : Exception(message);

public interface ICashRefundService
{
    Task<Guid?> LockOpenShiftAsync(Guid actorId, CancellationToken ct);
    Task StagePayoutAsync(Guid refundId, Guid shiftId, Guid actorId, decimal amount, CancellationToken ct);
}
