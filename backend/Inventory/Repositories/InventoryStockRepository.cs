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
                .ThenInclude(p => p.Category)
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
                EF.Functions.ILike(s.Product.Name, $"%{search}%") ||
                EF.Functions.ILike(s.Product.Sku, $"%{search}%") ||
                (s.Variant != null && EF.Functions.ILike(s.Variant.Sku, $"%{search}%")) ||
                (s.Variant != null && EF.Functions.ILike(s.Variant.VariantName, $"%{search}%")));
        }

        if (!string.IsNullOrWhiteSpace(criteria.CategoryName))
        {
            query = query.Where(s => s.Product.Category != null && s.Product.Category.Name == criteria.CategoryName);
        }

        if (criteria.VariantId.HasValue)
        {
            query = query.Where(s => s.VariantId == criteria.VariantId.Value);
        }

        if (criteria.LowStock.HasValue && criteria.LowStock.Value)
        {
            query = query.Where(s => s.Quantity - s.ReservedQuantity <= s.MinStockAlert);
        }

        if (criteria.OutOfStock == true)
        {
            query = query.Where(s => s.Quantity - s.ReservedQuantity <= 0);
        }

        if (criteria.InStock == true)
        {
            query = query.Where(s => s.Quantity - s.ReservedQuantity > s.MinStockAlert);
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

        if (items.Count > 0)
        {
            var productIds = items.Select(s => s.ProductId).Distinct().ToArray();
            var serialCounts = await dbContext.SerialImeis
                .AsNoTracking()
                .Where(s => productIds.Contains(s.ProductId))
                .GroupBy(s => new { s.ProductId, s.VariantId })
                .Select(group => new
                {
                    group.Key.ProductId,
                    group.Key.VariantId,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            foreach (var stock in items)
            {
                stock.RelatedSerialCount = serialCounts
                    .Where(count => count.ProductId == stock.ProductId && count.VariantId == stock.VariantId)
                    .Select(count => count.Count)
                    .FirstOrDefault();
            }
        }

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

    public Task<InventoryStock?> GetByIdForUpdateAsync(
        Guid stockId,
        CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks
            .FromSqlInterpolated($"SELECT * FROM inventory_stocks WHERE id = {stockId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<InventoryStock?> GetDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        var stock = await dbContext.InventoryStocks
            .AsNoTracking()
            .Include(stock => stock.Product)
                .ThenInclude(product => product.Category)
            .Include(stock => stock.Variant)
            .Include(stock => stock.Movements.OrderByDescending(movement => movement.CreatedAt).Take(10))
                .ThenInclude(movement => movement.PerformedByUser)
            .FirstOrDefaultAsync(stock => stock.Id == stockId, cancellationToken);

        if (stock is not null)
        {
            stock.RelatedSerialCount = await dbContext.SerialImeis
                .AsNoTracking()
                .CountAsync(serial => serial.ProductId == stock.ProductId && serial.VariantId == stock.VariantId, cancellationToken);
        }

        return stock;
    }

    public async Task<IReadOnlyList<SerialImei>> GetSerialsForUpdateAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        if (variantId.HasValue)
        {
            return await dbContext.SerialImeis
                .FromSqlInterpolated($"SELECT * FROM serial_imeis WHERE product_id = {productId} AND variant_id = {variantId.Value} FOR UPDATE")
                .ToListAsync(cancellationToken);
        }

        return await dbContext.SerialImeis
            .FromSqlInterpolated($"SELECT * FROM serial_imeis WHERE product_id = {productId} AND variant_id IS NULL FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMovementsAsync(Guid stockId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryMovements.CountAsync(movement => movement.InventoryStockId == stockId, cancellationToken);

    public void Add(InventoryStock stock)
    {
        dbContext.InventoryStocks.Add(stock);
    }

    public void AddMovement(InventoryMovement movement)
    {
        dbContext.InventoryMovements.Add(movement);
    }

    public void Delete(InventoryStock stock) => dbContext.InventoryStocks.Remove(stock);

    public void DeleteSerials(IEnumerable<SerialImei> serials) => dbContext.SerialImeis.RemoveRange(serials);
}
