using System;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Inventory;

public sealed class InventoryChangeNotifier : IInventoryChangeNotifier
{
    public event EventHandler<StockBalanceKey>? StockChanged;

    public void NotifyStockChanged(StockBalanceKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        StockChanged?.Invoke(this, key);
    }
}
