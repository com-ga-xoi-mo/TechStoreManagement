using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Repositories;

public class PosSessionRepository(AppDbContext dbContext) : IPosSessionRepository
{
    public Task<PosSession?> GetOpenByCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default) =>
        dbContext.PosSessions
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.CashierUserId == cashierUserId && s.Status == PosSessionStatus.Open, cancellationToken);

    public Task<PosSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PosSessions
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<PosSession?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PosSessions
            .FromSqlInterpolated($"SELECT * FROM pos_sessions WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<PosSession> Items, int TotalCount)> SearchAsync(
        PosSessionSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PosSessions.AsNoTracking().AsQueryable();
        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        if (criteria.CashierUserId.HasValue)
        {
            query = query.Where(s => s.CashierUserId == criteria.CashierUserId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(s => s.CashierUser)
            .OrderByDescending(s => s.OpenedAt)
            .ThenBy(s => s.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public void Add(PosSession session) => dbContext.PosSessions.Add(session);
}
