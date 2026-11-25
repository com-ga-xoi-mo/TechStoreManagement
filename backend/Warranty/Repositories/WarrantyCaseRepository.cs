using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;

namespace TechStore.Api.Warranty.Repositories;

public interface IWarrantyCaseRepository
{
    Task<WarrantyCase?> GetAsync(Guid id, CancellationToken ct);
    Task<WarrantyCaseDetails?> GetDetailAsync(Guid id, CancellationToken ct);
    Task<WarrantyCase?> LockAsync(Guid id, CancellationToken ct);
    Task<SerialImei?> FindDeviceByCodeAsync(string code, CancellationToken ct);
    Task<OrderItem?> FindSaleAsync(SerialImei device, CancellationToken ct);
    Task<int> ReturnedQuantityAsync(Guid itemId, CancellationToken ct);
    Task<bool> HasOpenReturnRequestAsync(Guid itemId, Guid deviceId, CancellationToken ct);
    Task<string?> GetLastCaseCodeAsync(string prefix, CancellationToken ct);
    Task<(IReadOnlyList<WarrantyCaseListRow> Items, int TotalCount)> SearchAsync(WarrantySearchCriteria criteria, CancellationToken ct);
    Task<IReadOnlyList<WarrantyCase>> CasesForDeviceAsync(Guid deviceId, CancellationToken ct);
    void Add(WarrantyCase claim);
    void AddEvent(WarrantyCaseEvent history);
}

public sealed record WarrantySearchCriteria(WarrantyCaseStatus? Status, string? Query, int Page, int PageSize);
public sealed record WarrantyCaseListRow(WarrantyCase Claim, string ProductName, string? DeviceCode, string OrderCode, string? CustomerName);
public sealed record WarrantyCaseDetails(WarrantyCase Claim, OrderItem Sale, SerialImei Device,
    string? ReplacementDeviceCode, IReadOnlyDictionary<Guid, string> ActorNames);

public sealed class WarrantyCaseRepository(AppDbContext db, IInventoryAfterSaleRepository afterSale) : IWarrantyCaseRepository
{
    public Task<WarrantyCase?> GetAsync(Guid id, CancellationToken ct) =>
        db.Set<WarrantyCase>().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<WarrantyCaseDetails?> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var claim = await db.Set<WarrantyCase>().AsNoTracking().Include(c => c.Events).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (claim == null)
            return null;

        var sale = await Sales().SingleAsync(item => item.Id == claim.OrderItemId, ct);
        var device = await db.SerialImeis.AsNoTracking().SingleAsync(x => x.Id == claim.DeviceId, ct);
        string? replacementCode = claim.ReplacementDeviceId is Guid replacementId
            ? await db.SerialImeis.AsNoTracking().Where(x => x.Id == replacementId)
                .Select(x => x.Imei ?? x.SerialNumber).SingleAsync(ct)
            : null;
        var actorIds = claim.Events.Select(e => e.ActorId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, ct);
        return new WarrantyCaseDetails(claim, sale, device, replacementCode, names);
    }

    public async Task<SerialImei?> FindDeviceByCodeAsync(string code, CancellationToken ct)
    {
        var devices = db.SerialImeis.AsNoTracking().Include(x => x.Product).Include(x => x.Variant);
        if (code.Length == 15 && code.All(char.IsAsciiDigit))
        {
            var imei = await devices.SingleOrDefaultAsync(x => x.Imei == code, ct);
            if (imei != null)
                return imei;
        }
        return await devices.SingleOrDefaultAsync(x => x.SerialNumber == code, ct);
    }

    public async Task<OrderItem?> FindSaleAsync(SerialImei device, CancellationToken ct)
    {
        var itemId = device.SaleOrderItemId ?? await afterSale.FindLegacySaleAsync(device.Id, ct);
        return itemId is Guid id ? await Sales().SingleOrDefaultAsync(item => item.Id == id, ct) : null;
    }

    private IQueryable<OrderItem> Sales() => db.OrderItems.AsNoTracking().Include(x => x.Order).ThenInclude(x => x.Customer);

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

    public Task<bool> HasOpenReturnRequestAsync(Guid itemId, Guid deviceId, CancellationToken ct) =>
        db.Set<ReturnCase>().AsNoTracking().AnyAsync(
            r => r.Lines.Any(line => line.OrderItemId == itemId)
                 && (r.Status == ReturnStatus.AwaitingBank
                     || (r.Status == ReturnStatus.Pending && !db.Set<WarrantyCase>().AsNoTracking().Any(
                         claim => claim.OrderItemId == itemId && claim.ReplacementDeviceId == deviceId
                             && claim.DeviceId != deviceId && claim.Status == WarrantyCaseStatus.Replaced))), ct);

    public Task<string?> GetLastCaseCodeAsync(string prefix, CancellationToken ct) =>
        db.Set<WarrantyCase>().AsNoTracking().Where(c => c.CaseCode.StartsWith(prefix))
            .OrderByDescending(c => c.CaseCode.Length).ThenByDescending(c => c.CaseCode)
            .Select(c => c.CaseCode).FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<WarrantyCaseListRow> Items, int TotalCount)> SearchAsync(
        WarrantySearchCriteria criteria, CancellationToken ct)
    {
        var cases = db.Set<WarrantyCase>().AsNoTracking();
        if (criteria.Status is { } status)
            cases = cases.Where(c => c.Status == status);

        if (criteria.Query is { Length: > 0 } search)
        {
            var pattern = "%" + search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            cases = cases.Where(c => EF.Functions.ILike(c.CaseCode, pattern, @"\")
                || (c.ContactPhone != null && EF.Functions.ILike(c.ContactPhone, pattern, @"\"))
                || db.Orders.AsNoTracking().Any(o => o.Id == c.OrderId && EF.Functions.ILike(o.OrderCode, pattern, @"\"))
                || db.SerialImeis.AsNoTracking().Any(d => d.Id == c.DeviceId
                    && ((d.Imei != null && EF.Functions.ILike(d.Imei, pattern, @"\"))
                        || (d.SerialNumber != null && EF.Functions.ILike(d.SerialNumber, pattern, @"\")))));
        }

        var total = await cases.CountAsync(ct);
        var skip = (criteria.Page - 1L) * criteria.PageSize;
        if (skip >= total)
            return ([], total);

        var page = cases.OrderByDescending(c => c.OpenedAt).ThenByDescending(c => c.Id)
            .Skip((int)skip).Take(criteria.PageSize);
        var items = await (from claim in page
                           join item in db.OrderItems.AsNoTracking() on claim.OrderItemId equals item.Id
                           join device in db.SerialImeis.AsNoTracking() on claim.DeviceId equals device.Id
                           join order in db.Orders.AsNoTracking() on claim.OrderId equals order.Id
                           join customer in db.Customers.AsNoTracking() on claim.CustomerId equals (Guid?)customer.Id into customers
                           from customer in customers.DefaultIfEmpty()
                           orderby claim.OpenedAt descending, claim.Id descending
                           select new WarrantyCaseListRow(claim, item.ProductName, device.Imei ?? device.SerialNumber,
                               order.OrderCode, customer == null ? null : customer.FullName)).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<WarrantyCase>> CasesForDeviceAsync(Guid deviceId, CancellationToken ct) =>
        await db.Set<WarrantyCase>().AsNoTracking().Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.OpenedAt).ThenByDescending(c => c.Id).ToListAsync(ct);

    public void Add(WarrantyCase claim) => db.Add(claim);
    public void AddEvent(WarrantyCaseEvent history) => db.Add(history);
}
