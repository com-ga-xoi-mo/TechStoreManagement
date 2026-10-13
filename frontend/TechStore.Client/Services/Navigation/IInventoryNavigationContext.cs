using System;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Navigation;

public interface IInventoryNavigationContext
{
    StockBalanceKey? PendingSerialImeiKey { get; }
    event EventHandler<StockBalanceKey>? SerialImeiRequested;
    void RequestSerialImei(StockBalanceKey key);
    StockBalanceKey? ConsumePendingSerialImeiKey();
}
