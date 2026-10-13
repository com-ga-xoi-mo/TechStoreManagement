using System;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Navigation;

public sealed class InventoryNavigationContext : IInventoryNavigationContext
{
    public StockBalanceKey? PendingSerialImeiKey { get; private set; }
    public event EventHandler<StockBalanceKey>? SerialImeiRequested;

    public void RequestSerialImei(StockBalanceKey key)
    {
        PendingSerialImeiKey = key ?? throw new ArgumentNullException(nameof(key));
        SerialImeiRequested?.Invoke(this, key);
    }

    public StockBalanceKey? ConsumePendingSerialImeiKey()
    {
        var key = PendingSerialImeiKey;
        PendingSerialImeiKey = null;
        return key;
    }
}
