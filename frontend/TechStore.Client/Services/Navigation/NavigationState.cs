namespace TechStore.Client.Services.Navigation;

public enum NavigationOutcome
{
    Navigated,
    AlreadyCurrent,
    UnknownDestination,
    DestinationUnavailable
}

/// <summary>Pure route-selection policy; presentation creation is supplied by the UI adapter.</summary>
public sealed class NavigationState(ShellDestination initialDestination = ShellDestination.Dashboard)
{
    public AppRoute CurrentRoute { get; private set; } = AppRoute.Root(ShellDestinations.Get(initialDestination).Id);
    public ShellDestination CurrentDestination => CurrentRoute.Destination;

    public NavigationOutcome TryNavigate(
        AppRoute route,
        Func<AppRoute, bool> prepareDestination)
    {
        ArgumentNullException.ThrowIfNull(prepareDestination);
        ArgumentNullException.ThrowIfNull(route);
        if (!Enum.IsDefined(route.Destination)) return NavigationOutcome.UnknownDestination;
        if (route.Equals(CurrentRoute)) return NavigationOutcome.AlreadyCurrent;
        if (!prepareDestination(route)) return NavigationOutcome.DestinationUnavailable;

        CurrentRoute = route;
        return NavigationOutcome.Navigated;
    }

    public NavigationOutcome TryNavigate(ShellDestination destination, Func<ShellDestination, bool> prepareDestination)
    {
        ArgumentNullException.ThrowIfNull(prepareDestination);
        if (!Enum.IsDefined(destination)) return NavigationOutcome.UnknownDestination;
        if (AppRoute.Root(destination).Equals(CurrentRoute)) return NavigationOutcome.AlreadyCurrent;
        if (!prepareDestination(destination)) return NavigationOutcome.DestinationUnavailable;
        CurrentRoute = AppRoute.Root(destination);
        return NavigationOutcome.Navigated;
    }
}
