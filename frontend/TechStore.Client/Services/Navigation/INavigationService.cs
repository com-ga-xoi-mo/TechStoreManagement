namespace TechStore.Client.Services.Navigation;

public interface INavigationService
{
    AppRoute CurrentRoute { get; }
    ShellDestination CurrentDestination { get; }
    event EventHandler<ShellDestination>? DestinationChanged;
    event EventHandler<AppRoute>? RouteChanged;
    NavigationOutcome Navigate(ShellDestination destination);
    NavigationOutcome Navigate(AppRoute route);
}

/// <summary>UI boundary that prepares a destination before the navigation state is committed.</summary>
public interface IDestinationPreparer
{
    bool Prepare(AppRoute route);
}
