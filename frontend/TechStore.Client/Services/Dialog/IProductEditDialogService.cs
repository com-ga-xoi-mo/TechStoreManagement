using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Services.Dialog;

public enum ProductEditDialogOutcome
{
    Cancelled,
    Failed,
    Saved
}

public sealed record ProductEditDialogResult(ProductEditDialogOutcome Outcome, ProductEditData? Product = null);

public interface IProductEditDialogService
{
    Task<ProductEditDialogResult> ShowAsync(string productId);
}
