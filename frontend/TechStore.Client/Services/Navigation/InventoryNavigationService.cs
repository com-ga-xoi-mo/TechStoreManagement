using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Navigation;

public sealed class InventoryNavigationService(
    INavigationService navigationService,
    IInventoryNavigationContext context) : IInventoryNavigationService
{
    public Task<bool> NavigateToSerialImeiAsync(StockBalanceKey key)
    {
        context.RequestSerialImei(key);
        return Task.FromResult(navigationService.NavigateTo(RouteId.Inventory));
    }
}
