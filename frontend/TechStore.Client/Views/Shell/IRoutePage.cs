using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.Views.Shell;

public interface IRoutePage
{
    PageViewModel ViewModel { get; }
}

public sealed class PreparedRoutePage : IDisposable
{
    private readonly PageActivationLease _lease;
    private readonly ILogger _logger;
    private bool _attached;

    public PreparedRoutePage(
        AppRoute route,
        Page page,
        PageViewModel viewModel,
        PageActivationLease lease,
        ILogger logger)
    {
        Route = route;
        Destination = route.Destination;
        Page = page;
        _lease = lease;
        _logger = logger;
        ViewModel = viewModel;
        _lease.ActivationFailed += OnActivationFailed;
        AttachLifecycle();
    }

    public ShellDestination Destination { get; }
    public AppRoute Route { get; }
    public Page Page { get; }
    public PageViewModel ViewModel { get; }

    public void Dispose()
    {
        _lease.ActivationFailed -= OnActivationFailed;
        if (_attached)
        {
            Page.Loaded -= OnLoaded;
            Page.Unloaded -= OnUnloaded;
            _attached = false;
        }

        Observe(_lease.Deactivate());
    }

    private void AttachLifecycle()
    {
        Page.Loaded += OnLoaded;
        Page.Unloaded += OnUnloaded;
        _attached = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) =>
        _lease.Start(cancellationToken => ViewModel.ActivateAsync(cancellationToken));

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_attached)
        {
            Page.Loaded -= OnLoaded;
            Page.Unloaded -= OnUnloaded;
            _attached = false;
        }

        Observe(_lease.Deactivate());
    }

    private void OnActivationFailed(object? sender, Exception exception) =>
        _logger.LogError(exception, "Route activation failed for {Route}.", Route);

    private void Observe(Task cleanup) => _ = ObserveAsync(cleanup);

    private async Task ObserveAsync(Task cleanup)
    {
        try
        {
            await cleanup.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Route cleanup failed for {Route}.", Route);
        }
    }
}
