namespace TechStore.Client.Services.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly NavigationState _state = new();
    private readonly IDestinationPreparer _preparer;

    public NavigationService(IDestinationPreparer preparer) =>
        _preparer = preparer ?? throw new ArgumentNullException(nameof(preparer));

    public ShellDestination CurrentDestination => _state.CurrentDestination;
    public AppRoute CurrentRoute => _state.CurrentRoute;

    public event EventHandler<ShellDestination>? DestinationChanged;
    public event EventHandler<AppRoute>? RouteChanged;

    public NavigationOutcome Navigate(ShellDestination destination) =>
        Enum.IsDefined(destination) ? Navigate(AppRoute.Root(destination)) : NavigationOutcome.UnknownDestination;

    public NavigationOutcome Navigate(AppRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var previousDestination = _state.CurrentDestination;
        var outcome = _state.TryNavigate(route, _preparer.Prepare);
        if (outcome == NavigationOutcome.Navigated)
        {
            RouteChanged?.Invoke(this, route);
            if (previousDestination != route.Destination)
                DestinationChanged?.Invoke(this, route.Destination);
        }
        return outcome;
    }
}
