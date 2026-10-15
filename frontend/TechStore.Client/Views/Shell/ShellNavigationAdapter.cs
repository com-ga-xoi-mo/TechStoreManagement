using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Shell;

namespace TechStore.Client.Views.Shell;

/// <summary>Adapts route-only navigation policy to the Shell's WinUI controls.</summary>
public sealed class ShellNavigationAdapter(
    INavigationService navigation,
    ShellDestinationPreparer preparer,
    ILogger<ShellNavigationAdapter> logger) : IDisposable
{
    private NavigationView? _navigationView;
    private ContentControl? _contentHost;
    private ShellViewModel? _viewModel;
    private PreparedRoutePage? _current;
    private bool _disposed;

    public void Attach(NavigationView navigationView, ContentControl contentHost, ShellViewModel viewModel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_navigationView is not null) return;

        _navigationView = navigationView ?? throw new ArgumentNullException(nameof(navigationView));
        _contentHost = contentHost ?? throw new ArgumentNullException(nameof(contentHost));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _navigationView.ItemInvoked += OnItemInvoked;
        navigation.RouteChanged += OnRouteChanged;

        if (preparer.Prepare(navigation.CurrentRoute) && preparer.TakePrepared() is { } initial)
        {
            _current = initial;
            _contentHost.Content = initial.Page;
            SynchronizeSelection(navigation.CurrentDestination);
        }
        else
        {
            logger.LogWarning("Initial navigation route {Route} could not be prepared at attach time.", navigation.CurrentRoute);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachEvents();
        if (_contentHost is not null)
            _contentHost.Content = null;
        _current?.Dispose();
        _current = null;

        var pending = preparer.TakePrepared();
        pending?.Dispose();
    }

    public Control? FocusCurrentPage()
    {
        if (_current?.Page is Control page && page.IsLoaded && page.Focus(FocusState.Programmatic))
            return page;

        if (_contentHost is { IsLoaded: true } && _contentHost.Focus(FocusState.Programmatic))
            return _contentHost;

        if (_navigationView?.SelectedItem is Control selected && selected.IsLoaded && selected.Focus(FocusState.Programmatic))
            return selected;

        return null;
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string route)
            return;

        if (ShellDestinations.TryGet(route, out var destination))
        {
            _viewModel!.NavigateCommand.Execute(destination.Id);
        }
    }

    private void OnRouteChanged(object? sender, AppRoute route)
    {
        var next = preparer.TakePrepared();
        if (next is null || !next.Route.Equals(route))
        {
            next?.Dispose();
            logger.LogError("Navigation committed to {Route} without its prepared page.", route);
            return;
        }

        var previous = _current;
        _current = next;
        _contentHost!.Content = next.Page;
        SynchronizeSelection(route.Destination);
        previous?.Dispose();
    }

    private void SynchronizeSelection(ShellDestination destination)
    {
        if (_navigationView is null) return;
        var selected = _navigationView.MenuItems
            .Cast<object>()
            .Concat(_navigationView.FooterMenuItems.Cast<object>())
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => ShellDestinations.TryGet(item.Tag as string, out var info) && info.Id == destination);
        _navigationView.SelectedItem = selected;
    }

    private void DetachEvents()
    {
        if (_navigationView is not null)
        {
            _navigationView.ItemInvoked -= OnItemInvoked;
        }

        navigation.RouteChanged -= OnRouteChanged;
        _navigationView = null;
        _contentHost = null;
        _viewModel = null;
    }
}
