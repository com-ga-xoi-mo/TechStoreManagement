using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Repositories;

public record VietQrHoldOwner(Guid OrderId, Guid? ShiftId);

public record VietQrSearchCriteria(VietQrStatus? Status, Guid? PosSessionId, Guid? CashierUserId, int Page, int PageSize);

public record VietQrEventSearchCriteria(VietQrEventStatus? Status, int Page, int PageSize);

public interface IVietQrHoldRepository
{
    Task<VietQrHoldOwner?> FindOwnerAsync(Guid qrId, CancellationToken ct);
    Task<IReadOnlyList<VietQrTransaction>> LockPaymentsAsync(Guid orderId, CancellationToken ct);

    // Read-only request with its order and the cashier who sells it, or null.
    Task<VietQrTransaction?> GetByIdAsync(Guid id, CancellationToken ct);

    // Read-only newest request of an order (any status), or null.
    Task<VietQrTransaction?> GetLatestByOrderAsync(Guid orderId, CancellationToken ct);

    // Read-only request carrying this transfer memo, or null.
    Task<VietQrTransaction?> FindByTransferCodeAsync(string transactionCode, CancellationToken ct);

    Task<(IReadOnlyList<VietQrTransaction> Items, int TotalCount)> SearchAsync(VietQrSearchCriteria criteria, CancellationToken ct);

    // Read-only waiting requests of a shift. The caller holds the shift lock when the answer must not change.
    Task<IReadOnlyList<VietQrTransaction>> ListPendingByShiftAsync(Guid shiftId, CancellationToken ct);

    // Ids of waiting requests whose deadline passed, oldest first.
    Task<IReadOnlyList<Guid>> ListDueIdsAsync(DateTime nowUtc, int take, CancellationToken ct);

    // True when another request already carries this bank reference.
    Task<bool> ReferenceUsedAsync(string provider, string reference, Guid exceptQrId, CancellationToken ct);

    void Add(VietQrTransaction transaction);

    Task<VietQrBankEvent?> FindEventAsync(string provider, string providerTransactionId, CancellationToken ct);
    Task<VietQrBankEvent?> GetEventForUpdateAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<VietQrBankEvent> Items, int TotalCount)> SearchEventsAsync(VietQrEventSearchCriteria criteria, CancellationToken ct);
    void Add(VietQrBankEvent bankEvent);
}

public sealed class VietQrHoldRepository(AppDbContext db) : IVietQrHoldRepository
{
    public Task<VietQrHoldOwner?> FindOwnerAsync(Guid qrId, CancellationToken ct) =>
        db.VietQrTransactions.AsNoTracking().Where(q => q.Id == qrId).Select(q => new VietQrHoldOwner(q.OrderId, q.PosSessionId)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<VietQrTransaction>> LockPaymentsAsync(Guid orderId, CancellationToken ct) =>
        await db.VietQrTransactions.FromSqlInterpolated(
            $"SELECT * FROM vietqr_transactions WHERE order_id = {orderId} ORDER BY id FOR UPDATE").ToListAsync(ct);

    public Task<VietQrTransaction?> GetByIdAsync(Guid id, CancellationToken ct) =>
        WithOrder().FirstOrDefaultAsync(q => q.Id == id, ct);

    public Task<VietQrTransaction?> GetLatestByOrderAsync(Guid orderId, CancellationToken ct) =>
        WithOrder().Where(q => q.OrderId == orderId).OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id).FirstOrDefaultAsync(ct);

    public Task<VietQrTransaction?> FindByTransferCodeAsync(string transactionCode, CancellationToken ct) =>
        WithOrder().FirstOrDefaultAsync(q => q.TransactionCode == transactionCode, ct);

    public async Task<(IReadOnlyList<VietQrTransaction> Items, int TotalCount)> SearchAsync(VietQrSearchCriteria criteria, CancellationToken ct)
    {
        var query = WithOrder();
        if (criteria.Status.HasValue) query = query.Where(q => q.Status == criteria.Status.Value);
        if (criteria.PosSessionId.HasValue) query = query.Where(q => q.PosSessionId == criteria.PosSessionId.Value);
        if (criteria.CashierUserId.HasValue) query = query.Where(q => q.Order.CashierUserId == criteria.CashierUserId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .ThenBy(q => q.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<VietQrTransaction>> ListPendingByShiftAsync(Guid shiftId, CancellationToken ct) =>
        await WithOrder().Where(q => q.PosSessionId == shiftId && q.Status == VietQrStatus.Pending).OrderBy(q => q.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListDueIdsAsync(DateTime nowUtc, int take, CancellationToken ct) =>
        await db.VietQrTransactions.AsNoTracking()
            .Where(q => q.Status == VietQrStatus.Pending && q.ExpiresAt <= nowUtc)
            .OrderBy(q => q.ExpiresAt).ThenBy(q => q.Id)
            .Select(q => q.Id)
            .Take(take)
            .ToListAsync(ct);

    public Task<bool> ReferenceUsedAsync(string provider, string reference, Guid exceptQrId, CancellationToken ct) =>
        db.VietQrTransactions.AsNoTracking()
            .AnyAsync(q => q.Provider == provider && q.ProviderTransactionId == reference && q.Id != exceptQrId, ct);

    public void Add(VietQrTransaction transaction) => db.VietQrTransactions.Add(transaction);

    public Task<VietQrBankEvent?> FindEventAsync(string provider, string providerTransactionId, CancellationToken ct) =>
        db.VietQrBankEvents.AsNoTracking()
            .Include(e => e.VietQrTransaction!).ThenInclude(q => q.Order)
            .FirstOrDefaultAsync(e => e.Provider == provider && e.ProviderTransactionId == providerTransactionId, ct);

    public Task<VietQrBankEvent?> GetEventForUpdateAsync(Guid id, CancellationToken ct) =>
        db.VietQrBankEvents.FromSqlInterpolated($"SELECT * FROM vietqr_bank_events WHERE id = {id} FOR UPDATE").FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<VietQrBankEvent> Items, int TotalCount)> SearchEventsAsync(VietQrEventSearchCriteria criteria, CancellationToken ct)
    {
        var query = db.VietQrBankEvents.AsNoTracking().Include(e => e.VietQrTransaction!).ThenInclude(q => q.Order).AsQueryable();
        if (criteria.Status.HasValue) query = query.Where(e => e.Status == criteria.Status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ReceivedAt)
            .ThenBy(e => e.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public void Add(VietQrBankEvent bankEvent) => db.VietQrBankEvents.Add(bankEvent);

    private IQueryable<VietQrTransaction> WithOrder() =>
        db.VietQrTransactions.AsNoTracking().Include(q => q.Order).ThenInclude(o => o.CashierUser);
}
