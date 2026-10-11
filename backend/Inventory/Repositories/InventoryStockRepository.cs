using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public class InventoryStockRepository(AppDbContext dbContext) : IInventoryStockRepository
{
    public async Task<(IReadOnlyList<InventoryStock> Items, int TotalCount)> SearchStocksAsync(
        InventoryStockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryStocks
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Variant)
            .AsQueryable();

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

        var totalCount = await query.CountAsync(cancellationToken);

        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize < 1 ? 20 : criteria.PageSize;

        var items = await query
            .OrderBy(s => s.Product.Name)
            .ThenBy(s => s.Variant != null ? s.Variant.VariantName : string.Empty)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<InventoryStock?> GetByProductAndVariantAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.InventoryStocks
            .Include(s => s.Product)
            .Include(s => s.Variant)
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.VariantId == variantId, cancellationToken);
    }

    public async Task<InventoryStock?> GetForUpdateAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        if (variantId.HasValue)
        {
            return await dbContext.InventoryStocks
                .FromSqlInterpolated($"SELECT * FROM inventory_stocks WHERE product_id = {productId} AND variant_id = {variantId.Value} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await dbContext.InventoryStocks
            .FromSqlInterpolated($"SELECT * FROM inventory_stocks WHERE product_id = {productId} AND variant_id IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(InventoryStock stock)
    {
        dbContext.InventoryStocks.Add(stock);
    }

    public void AddMovement(InventoryMovement movement)
    {
        dbContext.InventoryMovements.Add(movement);
    }
}
