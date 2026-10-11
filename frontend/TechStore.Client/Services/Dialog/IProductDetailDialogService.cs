using System.Threading.Tasks;

namespace TechStore.Client.Services.Dialog;

public interface IProductDetailDialogService
{
    Task ShowAsync(string productId);
}
