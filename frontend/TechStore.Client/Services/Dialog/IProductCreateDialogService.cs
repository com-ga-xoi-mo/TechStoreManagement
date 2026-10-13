using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Services.Dialog;

public enum ProductCreateDialogOutcome
{
    Cancelled,
    Failed,
    Created
}

public sealed record ProductCreateDialogResult(ProductCreateDialogOutcome Outcome, ProductSummary? Product = null);

public interface IProductCreateDialogService
{
    Task<ProductCreateDialogResult> ShowAsync();
}
