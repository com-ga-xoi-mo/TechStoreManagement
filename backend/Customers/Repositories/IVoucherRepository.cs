using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

public interface IVoucherRepository
{
    // Read-only voucher by upper-cased code, or null.
    Task<Voucher?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    // Tracked voucher for edits, or null.
    Task<Voucher?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Voucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Tracked voucher locked with SELECT ... FOR UPDATE so concurrent holds of the last use are serialized.
    // Requires an open transaction opened by the calling flow; outside one the lock is released immediately.
    Task<Voucher?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Voucher> Items, int TotalCount)> SearchAsync(VoucherSearchCriteria criteria, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    // Tracked use row of one order, or null.
    Task<VoucherRedemption?> GetRedemptionByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    void Add(Voucher voucher);
    void Add(VoucherRedemption redemption);
}
