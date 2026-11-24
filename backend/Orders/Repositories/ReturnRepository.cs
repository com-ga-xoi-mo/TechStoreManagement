using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Repositories;

public sealed class ReturnRepository(AppDbContext db) : IReturnRepository
{
    public async Task<ReturnCase?> FindAsync(Guid id, bool forUpdate, CancellationToken ct)
    {
        var cases = db.Set<ReturnCase>();
        if (!forUpdate)
            return await cases.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);

        var request = await cases.FromSqlInterpolated($"SELECT * FROM return_cases WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (request != null)
            await db.Entry(request).Collection(x => x.Lines).LoadAsync(ct);
        return request;
    }

    public Task<List<ReturnCase>> HistoryAsync(Guid orderId, CancellationToken ct) =>
        db.Set<ReturnCase>().AsNoTracking().Where(x => x.OrderId == orderId).Include(x => x.Lines)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

    public Task<List<OrderReturn>> SettledAsync(Guid orderId, CancellationToken ct) =>
        db.OrderReturns.AsNoTracking().Where(x => x.OrderId == orderId).ToListAsync(ct);

    public Task<bool> HasBankHoldAsync(Guid orderId, Guid except, CancellationToken ct) =>
        db.Set<ReturnCase>().AsNoTracking().AnyAsync(x => x.OrderId == orderId && x.Id != except &&
            x.Status == ReturnStatus.AwaitingBank, ct);

    public Task<List<ReturnCaseLine>> InspectionsAsync(CancellationToken ct) =>
        db.Set<ReturnCaseLine>().AsNoTracking().Where(x => x.Settlements.Any(s => s.Disposition != "Undelivered"))
            .Include(x => x.Item).ThenInclude(x => x.SerialImei).Include(x => x.Settlements)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

    public async Task<ReturnCaseLine?> LockInspectionAsync(Guid lineId, CancellationToken ct)
    {
        var line = await db.Set<ReturnCaseLine>()
            .FromSqlInterpolated($"SELECT * FROM return_case_lines WHERE id = {lineId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (line == null) return null;
        await db.Entry(line).Reference(x => x.Item).Query().Include(x => x.SerialImei).LoadAsync(ct);
        await db.Entry(line).Collection(x => x.Settlements).LoadAsync(ct);
        return line;
    }

    public void Add(ReturnCase request) => db.Add(request);
    public void AddSettlement(OrderReturn settlement) => db.OrderReturns.Add(settlement);
}
