using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public class SerialImeiRepository(AppDbContext dbContext) : ISerialImeiRepository
{
    public async Task<(IReadOnlyList<SerialImei> Items, int TotalCount)> SearchAsync(
        SerialImeiSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SerialImeis
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Variant)
            .AsQueryable();

        if (criteria.ProductId.HasValue)
        {
            query = query.Where(s => s.ProductId == criteria.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            query = query.Where(s =>
                (s.SerialNumber != null && EF.Functions.ILike(s.SerialNumber, $"%{search}%")) ||
                (s.Imei != null && EF.Functions.ILike(s.Imei, $"%{search}%")) ||
                EF.Functions.ILike(s.Product.Name, $"%{search}%") ||
                (s.Variant != null && EF.Functions.ILike(s.Variant.VariantName, $"%{search}%")));
        }

        if (criteria.VariantId.HasValue)
        {
            query = query.Where(s => s.VariantId == criteria.VariantId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize < 1 ? 20 : criteria.PageSize;

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ExistingIdentifiersResult> FindExistingIdentifiersAsync(
        IEnumerable<string> serialNumbers,
        IEnumerable<string> imeis,
        CancellationToken cancellationToken = default)
    {
        var serialList = serialNumbers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var imeiList = imeis
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingSerials = new List<string>();
        if (serialList.Count > 0)
        {
            existingSerials = await dbContext.SerialImeis
                .AsNoTracking()
                .Where(s => s.SerialNumber != null && serialList.Contains(s.SerialNumber))
                .Select(s => s.SerialNumber!)
                .ToListAsync(cancellationToken);
        }

        var existingImeis = new List<string>();
        if (imeiList.Count > 0)
        {
            existingImeis = await dbContext.SerialImeis
                .AsNoTracking()
                .Where(s => s.Imei != null && imeiList.Contains(s.Imei))
                .Select(s => s.Imei!)
                .ToListAsync(cancellationToken);
        }

        return new ExistingIdentifiersResult(existingSerials, existingImeis);
    }

    public async Task<IReadOnlyList<SerialImei>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        return await dbContext.SerialImeis
            .AsNoTracking()
            .Where(s => idList.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SerialImei>> GetForUpdateAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idArray = ids.Distinct().OrderBy(id => id).ToArray();
        if (idArray.Length == 0)
        {
            return [];
        }

        return await dbContext.SerialImeis
            .FromSqlInterpolated($"SELECT * FROM serial_imeis WHERE id = ANY({idArray}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public void AddRange(IEnumerable<SerialImei> items)
    {
        dbContext.SerialImeis.AddRange(items);
    }
}
