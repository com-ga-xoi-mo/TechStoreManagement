using TechStore.Client.ViewModels.Inventory;

namespace TechStore.Client.Services.Inventory;

/// <summary>Coordinates a delete dialog result with one authoritative workspace refresh.</summary>
public sealed class CategoryDeleteCoordinator
{
    public async Task<bool> PresentAsync(
        DeleteCategoryDialogViewModel viewModel,
        Func<Task<bool>> presentDialogAsync,
        Func<Task> refreshAsync)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(presentDialogAsync);
        ArgumentNullException.ThrowIfNull(refreshAsync);

        var deleted = false;
        void OnDeleted(object? sender, Guid _) => deleted = true;
        viewModel.CategoryDeleted += OnDeleted;
        try
        {
            await presentDialogAsync();
        }
        finally
        {
            viewModel.CategoryDeleted -= OnDeleted;
        }

        if (!deleted) return false;
        await refreshAsync();
        return true;
    }
}
