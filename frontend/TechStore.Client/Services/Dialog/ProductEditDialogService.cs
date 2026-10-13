using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.Views.Catalog;

namespace TechStore.Client.Services.Dialog;

public sealed class ProductEditDialogService(IServiceProvider services) : IProductEditDialogService
{
    public async Task<ProductEditDialogResult> ShowAsync(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        await DialogModalCoordinator.Gate.WaitAsync();

        ProductEditViewModel? viewModel = null;
        Control? focusTarget = null;
        try
        {
            var window = (Application.Current as App)?.GetMainWindow();
            var xamlRoot = window?.Content?.XamlRoot;
            if (xamlRoot is null)
            {
                return new ProductEditDialogResult(ProductEditDialogOutcome.Cancelled);
            }

            focusTarget = FocusManager.GetFocusedElement(xamlRoot) as Control;
            viewModel = services.GetRequiredService<ProductEditViewModel>();
            var dialog = new ProductEditDialog(viewModel) { XamlRoot = xamlRoot };
            _ = viewModel.LoadAsync(productId);
            await dialog.ShowAsync();

            if (viewModel.IsSaved)
            {
                return new ProductEditDialogResult(ProductEditDialogOutcome.Saved, viewModel.SavedProduct);
            }

            return viewModel.IsLoadError || viewModel.IsUnavailable || viewModel.IsSaveError
                ? new ProductEditDialogResult(ProductEditDialogOutcome.Failed)
                : new ProductEditDialogResult(ProductEditDialogOutcome.Cancelled);
        }
        finally
        {
            viewModel?.Dispose();
            focusTarget?.Focus(FocusState.Programmatic);
            DialogModalCoordinator.Gate.Release();
        }
    }
}
