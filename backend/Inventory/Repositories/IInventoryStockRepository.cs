using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface IInventoryStockRepository
{
    Task<(IReadOnlyList<InventoryStock> Items, int TotalCount)> SearchStocksAsync(
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

    void Add(InventoryStock stock);

    void AddMovement(InventoryMovement movement);
}
