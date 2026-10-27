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

    void Add(InventoryStock stock);

    void AddMovement(InventoryMovement movement);

    void Delete(InventoryStock stock);
}
