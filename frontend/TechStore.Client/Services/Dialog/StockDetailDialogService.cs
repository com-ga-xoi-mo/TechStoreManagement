using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Views.Inventory;

namespace TechStore.Client.Services.Dialog;

public sealed class StockDetailDialogService(IServiceProvider services) : IStockDetailDialogService
{
    public async Task ShowAsync(StockBalanceKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        await DialogModalCoordinator.Gate.WaitAsync();
        StockDetailViewModel? viewModel = null;
        try
        {
            var window = (Application.Current as App)?.GetMainWindow();
            var xamlRoot = window?.Content?.XamlRoot;
            if (xamlRoot is null)
            {
                return;
            }

            var focusTarget = FocusManager.GetFocusedElement(xamlRoot) as Control;
            viewModel = services.GetRequiredService<StockDetailViewModel>();
            var dialog = new StockDetailDialog(viewModel) { XamlRoot = xamlRoot };
            _ = viewModel.LoadAsync(key);
            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                viewModel.CancelLoad();
                focusTarget?.Focus(FocusState.Programmatic);
            }
        }
        finally
        {
            viewModel?.Dispose();
            DialogModalCoordinator.Gate.Release();
        }
    }
}
