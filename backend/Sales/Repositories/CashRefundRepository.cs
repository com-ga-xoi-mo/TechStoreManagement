using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Sales.Entities;

namespace TechStore.Api.Sales.Repositories;

public interface ICashRefundRepository
{
    Task<CashRefundPayout?> FindAsync(Guid refundId, CancellationToken ct);
    void Add(CashRefundPayout payout);
}

public sealed class CashRefundRepository(AppDbContext db) : ICashRefundRepository
{
    public Task<CashRefundPayout?> FindAsync(Guid refundId, CancellationToken ct) =>
        db.Set<CashRefundPayout>().AsNoTracking().SingleOrDefaultAsync(x => x.RefundId == refundId, ct);
    public void Add(CashRefundPayout payout) => db.Add(payout);
}
