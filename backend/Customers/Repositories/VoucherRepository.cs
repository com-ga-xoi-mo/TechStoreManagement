using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Repositories;

public class VoucherRepository(AppDbContext dbContext) : IVoucherRepository
{
    public Task<Voucher?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        dbContext.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == code, cancellationToken);

    public Task<Voucher?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Vouchers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<Voucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<Voucher?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Vouchers
            .FromSqlInterpolated($"SELECT * FROM vouchers WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Voucher> Items, int TotalCount)> SearchAsync(VoucherSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Vouchers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var pattern = $"%{EscapeLikePattern(criteria.Query)}%";
            query = query.Where(v => EF.Functions.ILike(v.Code, pattern, "!")
                || (v.Description != null && EF.Functions.ILike(v.Description, pattern, "!")));
        }
        if (criteria.IsActive.HasValue) query = query.Where(v => v.IsActive == criteria.IsActive.Value);

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(v => v.CreatedAt).ThenBy(v => v.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize)
            .ToListAsync(cancellationToken);
        return (items, count);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        dbContext.Vouchers.AsNoTracking().AnyAsync(v => v.Code == code, cancellationToken);

    public Task<VoucherRedemption?> GetRedemptionByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.VoucherRedemptions.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public void Add(Voucher voucher) => dbContext.Vouchers.Add(voucher);
    public void Add(VoucherRedemption redemption) => dbContext.VoucherRedemptions.Add(redemption);

    // Same escaping as customer search: ILIKE wildcards and the escape character itself are matched literally.
    private static string EscapeLikePattern(string value) =>
        value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
}
