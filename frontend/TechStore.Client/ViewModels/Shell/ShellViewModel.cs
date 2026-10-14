using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Services.Navigation;

namespace TechStore.Client.ViewModels.Shell;

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigation;

    [ObservableProperty] private ShellDestination selectedDestination;
    [ObservableProperty] private string currentTitle;
    [ObservableProperty] private string navigationError = string.Empty;

    public IReadOnlyList<ShellDestinationInfo> Destinations { get; } = ShellDestinations.All;
    public IReadOnlyList<ShellDestinationInfo> MainDestinations { get; } =
        ShellDestinations.All.Where(item => !item.IsFooterItem).ToArray();
    public ShellDestinationInfo SettingsDestination { get; } = ShellDestinations.Get(ShellDestination.Settings);
    public bool HasNavigationError => !string.IsNullOrWhiteSpace(NavigationError);

    public ShellViewModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        selectedDestination = navigation.CurrentDestination;
        currentTitle = ShellDestinations.Get(selectedDestination).Title;
        _navigation.DestinationChanged += OnDestinationChanged;
    }

    [RelayCommand]
    private void Navigate(ShellDestination destination)
    {
        NavigationError = string.Empty;
        var outcome = _navigation.Navigate(destination);
        if (outcome is NavigationOutcome.UnknownDestination or NavigationOutcome.DestinationUnavailable)
            NavigationError = "Không thể mở mục này. Hãy thử lại.";
    }

    public void Dispose() => _navigation.DestinationChanged -= OnDestinationChanged;

    partial void OnNavigationErrorChanged(string value) => OnPropertyChanged(nameof(HasNavigationError));

    private void OnDestinationChanged(object? sender, ShellDestination destination)
    {
        SelectedDestination = destination;
        CurrentTitle = ShellDestinations.Get(destination).Title;
        NavigationError = string.Empty;
    }
}
