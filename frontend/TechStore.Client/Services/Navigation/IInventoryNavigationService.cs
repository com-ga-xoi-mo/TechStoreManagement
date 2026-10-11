using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Navigation;

public interface IInventoryNavigationService
{
    Task<bool> NavigateToSerialImeiAsync(StockBalanceKey key);
}
