using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Repositories.Inventory;

public sealed class InventoryRepositoryUnavailableException(string message) : InvalidOperationException(message);

public interface IInventoryRepository
{
    Task<StockListPage> GetStockPageAsync(StockListQuery query, CancellationToken cancellationToken = default);

    Task<StockDetailSnapshot?> GetStockDetailAsync(
        StockBalanceKey key,
        CancellationToken cancellationToken = default);

    Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default);
}
