using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;

namespace TechStore.UnitTests;

public class FakeInventoryStockRepository : IInventoryStockRepository
{
    public List<InventoryStock> Stocks { get; } = [];
    public List<InventoryMovement> Movements { get; } = [];

    public Task<(IReadOnlyList<InventoryStock> Items, int TotalCount)> SearchStocksAsync(
        InventoryStockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = Stocks.AsQueryable();

        if (criteria.ProductId.HasValue)
        {
            query = query.Where(s => s.ProductId == criteria.ProductId.Value);
        }

        if (criteria.VariantId.HasValue)
        {
            query = query.Where(s => s.VariantId == criteria.VariantId.Value);
        }

        if (criteria.LowStock.HasValue && criteria.LowStock.Value)
        {
            query = query.Where(s => s.Quantity <= s.MinStockAlert);
        }

        var total = query.Count();
        var items = query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<InventoryStock>)items, total));
    }

    public Task<InventoryStock?> GetByProductAndVariantAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        var stock = Stocks.FirstOrDefault(s => s.ProductId == productId && s.VariantId == variantId);
        return Task.FromResult(stock);
    }

    public Task<InventoryStock?> GetForUpdateAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        var stock = Stocks.FirstOrDefault(s => s.ProductId == productId && s.VariantId == variantId);
        return Task.FromResult(stock);
    }

    public void Add(InventoryStock stock)
    {
        Stocks.Add(stock);
    }

    public void AddMovement(InventoryMovement movement)
    {
        Movements.Add(movement);
    }
}
