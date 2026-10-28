using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface IInventoryStockRepository
{
    Task<(IReadOnlyList<InventoryStockView> Items, int TotalCount)> SearchStocksAsync(
        InventoryStockSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<InventoryStock?> GetByProductAndVariantAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default);

    Task<InventoryStock?> GetForUpdateAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default);

    Task<InventoryStock?> GetByIdForUpdateAsync(
        Guid stockId,
        CancellationToken cancellationToken = default);

    Task<InventoryStockView?> GetDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default);

    Task<int> CountMovementsAsync(
        Guid stockId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Weighted average <c>unit_cost</c> of the purchase-order lines already received for this product/variant
    /// (weighted by <c>received_quantity</c>), or null when nothing was received. This is the real cost of
    /// goods without a serial/IMEI; <c>products.cost_price</c> is only a reference and is never used.
    /// </summary>
    Task<decimal?> GetAverageReceivedUnitCostAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default);

    void Add(InventoryStock stock);

    void AddMovement(InventoryMovement movement);

    void Delete(InventoryStock stock);
}
