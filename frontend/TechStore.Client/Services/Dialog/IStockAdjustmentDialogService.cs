using System.Threading.Tasks;
using TechStore.Client.Models.Inventory;

namespace TechStore.Client.Services.Dialog;

public interface IStockAdjustmentDialogService
{
    Task ShowAsync(StockBalance balance, bool isPreviewData);
}
