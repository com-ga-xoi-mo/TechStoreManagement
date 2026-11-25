using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;

namespace TechStore.UnitTests;

public class FakeVoucherRepository : IVoucherRepository
{
    public Dictionary<Guid, Voucher> Vouchers { get; } = new();
    public List<VoucherRedemption> Redemptions { get; } = [];
    public int ForUpdateCalls { get; private set; }

    public Task<Voucher?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vouchers.Values.FirstOrDefault(v => v.Code == code));

    public Task<Voucher?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<Voucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Vouchers.TryGetValue(id, out var voucher);
        return Task.FromResult(voucher);
    }

    public Task<Voucher?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ForUpdateCalls++;
        return GetByIdAsync(id, cancellationToken);
    }

    public Task<(IReadOnlyList<Voucher> Items, int TotalCount)> SearchAsync(VoucherSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        IEnumerable<Voucher> query = Vouchers.Values;
        if (criteria.Query != null)
            query = query.Where(v => v.Code.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase)
                || (v.Description?.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase) ?? false));
        if (criteria.IsActive.HasValue) query = query.Where(v => v.IsActive == criteria.IsActive.Value);
        var all = query.OrderByDescending(v => v.CreatedAt).ThenBy(v => v.Id).ToList();
        var page = all.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Voucher>, int)>((page, all.Count));
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vouchers.Values.Any(v => v.Code == code));

    public Task<VoucherRedemption?> GetRedemptionByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Redemptions.FirstOrDefault(r => r.OrderId == orderId));

    public void Add(Voucher voucher) => Vouchers[voucher.Id] = voucher;
    public void Add(VoucherRedemption redemption) => Redemptions.Add(redemption);
}
