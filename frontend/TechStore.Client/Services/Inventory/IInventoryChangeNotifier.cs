using System;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Inventory;

public interface IInventoryChangeNotifier
{
    event EventHandler<StockBalanceKey>? StockChanged;
    void NotifyStockChanged(StockBalanceKey key);
}
