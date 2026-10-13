using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Views.Inventory;

namespace TechStore.Client.Services.Dialog;

public sealed class StockAdjustmentDialogService(IServiceProvider services) : IStockAdjustmentDialogService
{
    public async Task ShowAsync(StockBalance balance, bool isPreviewData)
    {
        ArgumentNullException.ThrowIfNull(balance);
        await DialogModalCoordinator.Gate.WaitAsync();
        StockAdjustmentViewModel? viewModel = null;
        try
        {
            var window = (Application.Current as App)?.GetMainWindow();
            var xamlRoot = window?.Content?.XamlRoot;
            if (xamlRoot is null) return;

            var focusTarget = FocusManager.GetFocusedElement(xamlRoot) as Control;
            viewModel = services.GetRequiredService<StockAdjustmentViewModel>();
            viewModel.Initialize(balance, isPreviewData);
            var dialog = new StockAdjustmentDialog(viewModel) { XamlRoot = xamlRoot };
            void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName == nameof(StockAdjustmentViewModel.IsClosed) && viewModel.IsClosed)
                {
                    dialog.Hide();
                }
            }

            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                if (!viewModel.IsClosed)
                {
                    viewModel.CancelCommand.Execute(null);
                }
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
