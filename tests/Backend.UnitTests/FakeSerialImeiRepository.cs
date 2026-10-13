using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;

namespace TechStore.UnitTests;

public class FakeSerialImeiRepository : ISerialImeiRepository
{
    public List<SerialImei> Items { get; } = [];

    public Task<(IReadOnlyList<SerialImei> Items, int TotalCount)> SearchAsync(
        SerialImeiSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = Items.AsQueryable();

        if (criteria.ProductId.HasValue)
        {
            query = query.Where(s => s.ProductId == criteria.ProductId.Value);
        }

        if (criteria.VariantId.HasValue)
        {
            query = query.Where(s => s.VariantId == criteria.VariantId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        var total = query.Count();
        var paged = query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<SerialImei>)paged, total));
    }

    public Task<ExistingIdentifiersResult> FindExistingIdentifiersAsync(
        IEnumerable<string> serialNumbers,
        IEnumerable<string> imeis,
        CancellationToken cancellationToken = default)
    {
        var serialSet = new HashSet<string>(serialNumbers.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
        var imeiSet = new HashSet<string>(imeis.Where(i => !string.IsNullOrWhiteSpace(i)), StringComparer.OrdinalIgnoreCase);

        var existingSerials = Items
            .Where(s => s.SerialNumber != null && serialSet.Contains(s.SerialNumber))
            .Select(s => s.SerialNumber!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingImeis = Items
            .Where(s => s.Imei != null && imeiSet.Contains(s.Imei))
            .Select(s => s.Imei!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new ExistingIdentifiersResult(existingSerials, existingImeis));
    }

    public int GetByIdsCallCount { get; private set; }
    public int GetForUpdateCallCount { get; private set; }

    // Ids passed to each GetForUpdateAsync call, in the order the fake returned them (ascending id).
    public List<IReadOnlyList<Guid>> LockedIdBatches { get; } = [];

    public Task<IReadOnlyList<SerialImei>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        GetByIdsCallCount++;
        var idSet = ids.ToHashSet();
        IReadOnlyList<SerialImei> found = Items.Where(s => idSet.Contains(s.Id)).ToList();
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<SerialImei>> GetForUpdateAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        GetForUpdateCallCount++;
        var idSet = ids.ToHashSet();
        IReadOnlyList<SerialImei> found = Items.Where(s => idSet.Contains(s.Id)).OrderBy(s => s.Id).ToList();
        LockedIdBatches.Add(found.Select(s => s.Id).ToList());
        return Task.FromResult(found);
    }

    public void AddRange(IEnumerable<SerialImei> items)
    {
        Items.AddRange(items);
    }
}
