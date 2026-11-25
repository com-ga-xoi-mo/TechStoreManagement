using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface IWarrantyCaseRepository
{
    Task<WarrantyCase?> GetAsync(Guid id, CancellationToken ct);
    Task<WarrantyCase?> LockAsync(Guid id, CancellationToken ct);
    Task<int> ReturnedQuantityAsync(Guid itemId, CancellationToken ct);
    void Add(WarrantyCase claim);
}

public sealed class WarrantyCaseRepository(AppDbContext db) : IWarrantyCaseRepository
{
    public Task<WarrantyCase?> GetAsync(Guid id, CancellationToken ct) =>
        db.Set<WarrantyCase>().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<WarrantyCase?> LockAsync(Guid id, CancellationToken ct)
    {
        // Two requests can first use the same case ID on different sales, before a case row exists.
        var key = BitConverter.ToInt64(id.ToByteArray());
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        return await db.Set<WarrantyCase>().FromSqlInterpolated($"SELECT * FROM warranty_cases WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
    }

    public Task<int> ReturnedQuantityAsync(Guid itemId, CancellationToken ct) =>
        db.OrderReturns.AsNoTracking().Where(r => r.OrderItemId == itemId).SumAsync(r => r.Quantity, ct);

    public void Add(WarrantyCase claim) => db.Add(claim);
}
