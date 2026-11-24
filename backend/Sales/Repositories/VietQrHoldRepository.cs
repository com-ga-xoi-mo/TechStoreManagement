using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Sales.Entities;

namespace TechStore.Api.Sales.Repositories;

public record VietQrHoldOwner(Guid OrderId, Guid? ShiftId);

public interface IVietQrHoldRepository
{
    Task<VietQrHoldOwner?> FindOwnerAsync(Guid qrId, CancellationToken ct);
    Task<IReadOnlyList<VietQrTransaction>> LockPaymentsAsync(Guid orderId, CancellationToken ct);
}

public sealed class VietQrHoldRepository(AppDbContext db) : IVietQrHoldRepository
{
    public Task<VietQrHoldOwner?> FindOwnerAsync(Guid qrId, CancellationToken ct) =>
        db.VietQrTransactions.AsNoTracking().Where(q => q.Id == qrId).Select(q => new VietQrHoldOwner(q.OrderId, q.PosSessionId)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<VietQrTransaction>> LockPaymentsAsync(Guid orderId, CancellationToken ct) =>
        await db.VietQrTransactions.FromSqlInterpolated(
            $"SELECT * FROM vietqr_transactions WHERE order_id = {orderId} ORDER BY id FOR UPDATE").ToListAsync(ct);
}
