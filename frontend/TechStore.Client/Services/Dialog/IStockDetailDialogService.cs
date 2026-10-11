using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Dialog;

public interface IStockDetailDialogService
{
    Task ShowAsync(StockBalanceKey key);
}
