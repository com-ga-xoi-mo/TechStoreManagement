using System;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Repositories.Inventory;

public sealed class UnavailableInventoryRepository : IInventoryRepository
{
    public Task<StockListPage> GetStockPageAsync(
        StockListQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InventoryRepositoryUnavailableException(
            "Inventory API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<StockDetailSnapshot?> GetStockDetailAsync(
        StockBalanceKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InventoryRepositoryUnavailableException(
            "Inventory detail API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InventoryRepositoryUnavailableException(
            "Inventory adjustment API integration is not configured. Enable explicit preview mode to simulate sample behavior.");
    }
}
