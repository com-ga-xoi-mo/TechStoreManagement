using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.Views.Catalog;

namespace TechStore.Client.Services.Dialog;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message, string closeButtonText = "OK");
}

public sealed class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, string closeButtonText = "OK")
    {
        // Must get a XamlRoot. We pull it from the main window's current content.
        var window = (Application.Current as App)?.GetMainWindow();
        var xamlRoot = window?.Content?.XamlRoot;

        if (xamlRoot == null)
            return;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText,
            XamlRoot = xamlRoot
        };

        await DialogModalCoordinator.Gate.WaitAsync();
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            DialogModalCoordinator.Gate.Release();
        }
    }
}

public sealed class ProductDetailDialogService(IServiceProvider services) : IProductDetailDialogService
{
    public async Task ShowAsync(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        await DialogModalCoordinator.Gate.WaitAsync();

        ProductDetailViewModel? viewModel = null;
        try
        {
            var window = (Application.Current as App)?.GetMainWindow();
            var xamlRoot = window?.Content?.XamlRoot;
            if (xamlRoot is null)
            {
                return;
            }

            var focusTarget = FocusManager.GetFocusedElement(xamlRoot) as Control;
            viewModel = services.GetRequiredService<ProductDetailViewModel>();
            var dialog = new ProductDetailDialog(viewModel) { XamlRoot = xamlRoot };
            _ = viewModel.LoadAsync(productId);

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

internal static class DialogModalCoordinator
{
    internal static readonly SemaphoreSlim Gate = new(1, 1);
}
