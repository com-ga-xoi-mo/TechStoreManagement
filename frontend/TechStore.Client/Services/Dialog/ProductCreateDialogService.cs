using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.Views.Catalog;

namespace TechStore.Client.Services.Dialog;

public sealed class ProductCreateDialogService(IServiceProvider services) : IProductCreateDialogService
{
    public async Task<ProductCreateDialogResult> ShowAsync()
    {
        await DialogModalCoordinator.Gate.WaitAsync();

        try
        {
            var window = (Application.Current as App)?.GetMainWindow();
            var xamlRoot = window?.Content?.XamlRoot;
            if (xamlRoot is null)
            {
                return new ProductCreateDialogResult(ProductCreateDialogOutcome.Cancelled);
            }

            var focusTarget = FocusManager.GetFocusedElement(xamlRoot) as Control;
            var viewModel = services.GetRequiredService<ProductCreateViewModel>();
            await viewModel.LoadCategoriesAsync();
            var dialog = new ProductCreateDialog(viewModel) { XamlRoot = xamlRoot };

            try
            {
                await dialog.ShowAsync();
                if (viewModel.IsCreated && viewModel.CreatedProduct is not null)
                {
                    return new ProductCreateDialogResult(ProductCreateDialogOutcome.Created, viewModel.CreatedProduct);
                }

                return viewModel.IsSubmissionError
                    ? new ProductCreateDialogResult(ProductCreateDialogOutcome.Failed)
                    : new ProductCreateDialogResult(ProductCreateDialogOutcome.Cancelled);
            }
            finally
            {
                focusTarget?.Focus(FocusState.Programmatic);
            }
        }
        finally
        {
            DialogModalCoordinator.Gate.Release();
        }
    }
}
