using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;

namespace TechStore.UnitTests;

public class FakeInventoryStockRepository : IInventoryStockRepository
{
    public List<InventoryStock> Stocks { get; } = [];
    public List<InventoryMovement> Movements { get; } = [];
    public List<SerialImei> Serials { get; } = [];

    // Stock rows locked through GetForUpdateAsync, in call order (used to assert the lock order).
    public List<(Guid ProductId, Guid? VariantId)> LockedKeys { get; } = [];

    public Task<(IReadOnlyList<InventoryStockView> Items, int TotalCount)> SearchStocksAsync(
        InventoryStockSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = Stocks.AsQueryable();

        if (criteria.ProductId.HasValue)
        {
            query = query.Where(s => s.ProductId == criteria.ProductId.Value);
        }

        if (criteria.CategoryId.HasValue)
        {
            query = query.Where(s => s.Product != null && s.Product.CategoryId == criteria.CategoryId.Value);
        }

        if (criteria.VariantId.HasValue)
        {
            query = query.Where(s => s.VariantId == criteria.VariantId.Value);
        }

        if (criteria.LowStock.HasValue && criteria.LowStock.Value)
        {
            query = query.Where(s => s.Quantity - s.ReservedQuantity > 0 && s.Quantity - s.ReservedQuantity <= s.MinStockAlert);
        }

        if (criteria.OutOfStock == true)
        {
            query = query.Where(s => s.Quantity - s.ReservedQuantity <= 0);
        }

        if (criteria.InStock == true)
        {
            query = query.Where(s => s.Quantity - s.ReservedQuantity > s.MinStockAlert);
        }

        var total = query.Count();
        var items = query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(s => new InventoryStockView(
                s,
                Serials.Count(serial => serial.ProductId == s.ProductId && serial.VariantId == s.VariantId)))
            .ToList();

        return Task.FromResult(((IReadOnlyList<InventoryStockView>)items, total));
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
        LockedKeys.Add((productId, variantId));
        var stock = Stocks.FirstOrDefault(s => s.ProductId == productId && s.VariantId == variantId);
        return Task.FromResult(stock);
    }

    public Task<InventoryStock?> GetByIdForUpdateAsync(
        Guid stockId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Stocks.FirstOrDefault(stock => stock.Id == stockId));

    public Task<InventoryStockView?> GetDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        var stock = Stocks.FirstOrDefault(candidate => candidate.Id == stockId);
        if (stock is null)
        {
            return Task.FromResult<InventoryStockView?>(null);
        }

        stock.Movements = Movements.Where(movement => movement.InventoryStockId == stockId).ToList();
        var count = Serials.Count(serial => serial.ProductId == stock.ProductId && serial.VariantId == stock.VariantId);
        return Task.FromResult<InventoryStockView?>(new InventoryStockView(stock, count));
    }

    public Task<int> CountMovementsAsync(
        Guid stockId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Movements.Count(movement => movement.InventoryStockId == stockId));

    // Average received purchase cost per stock key; a missing key means no purchase line was received.
    public Dictionary<(Guid ProductId, Guid? VariantId), decimal> ReceivedUnitCosts { get; } = [];

    public Task<decimal?> GetAverageReceivedUnitCostAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceivedUnitCosts.TryGetValue((productId, variantId), out var cost) ? (decimal?)cost : null);

    public void Add(InventoryStock stock)
    {
        Stocks.Add(stock);
    }

    public void AddMovement(InventoryMovement movement)
    {
        Movements.Add(movement);
        if (movement.InventoryStock is null)
            movement.InventoryStock = Stocks.FirstOrDefault(stock => stock.Id == movement.InventoryStockId)!;
    }

    public void Delete(InventoryStock stock) => Stocks.Remove(stock);
}
