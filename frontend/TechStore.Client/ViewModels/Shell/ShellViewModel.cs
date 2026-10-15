using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Shell;

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly ISessionStore _sessionStore;

    [ObservableProperty] private ShellDestination selectedDestination;
    [ObservableProperty] private string currentTitle;
    [ObservableProperty] private string navigationError = string.Empty;
    [ObservableProperty] private string userDisplayName = "Tài khoản phát triển";
    [ObservableProperty] private string userRoleDescription = "Chưa đăng nhập";
    [ObservableProperty] private string userAvatarInitials = "TS";
    [ObservableProperty] private bool isAuthenticated;

    public IReadOnlyList<ShellDestinationInfo> Destinations { get; } = ShellDestinations.All;
    public IReadOnlyList<ShellDestinationInfo> MainDestinations { get; } =
        ShellDestinations.All.Where(item => !item.IsFooterItem).ToArray();
    public ShellDestinationInfo SettingsDestination { get; } = ShellDestinations.Get(ShellDestination.Settings);
    public bool HasNavigationError => !string.IsNullOrWhiteSpace(NavigationError);

    public ShellViewModel(INavigationService navigation, ISessionStore sessionStore)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        selectedDestination = navigation.CurrentDestination;
        currentTitle = ShellDestinations.Get(selectedDestination).Title;
        _navigation.DestinationChanged += OnDestinationChanged;
        _sessionStore.SessionChanged += OnSessionChanged;
        UpdateSessionInfo(_sessionStore.Current);
    }

    [RelayCommand]
    private void Navigate(ShellDestination destination)
    {
        NavigationError = string.Empty;
        var outcome = _navigation.Navigate(destination);
        if (outcome is NavigationOutcome.UnknownDestination or NavigationOutcome.DestinationUnavailable)
            NavigationError = "Không thể mở mục này. Hãy thử lại.";
    }

    public void Dispose()
    {
        _navigation.DestinationChanged -= OnDestinationChanged;
        _sessionStore.SessionChanged -= OnSessionChanged;
    }

    partial void OnNavigationErrorChanged(string value) => OnPropertyChanged(nameof(HasNavigationError));

    private void OnDestinationChanged(object? sender, ShellDestination destination)
    {
        SelectedDestination = destination;
        CurrentTitle = ShellDestinations.Get(destination).Title;
        NavigationError = string.Empty;
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        UpdateSessionInfo(_sessionStore.Current);
    }

    private void UpdateSessionInfo(SessionSnapshot session)
    {
        if (session.Status == SessionStatus.Active && !string.IsNullOrEmpty(session.Username))
        {
            IsAuthenticated = true;
            UserDisplayName = !string.IsNullOrEmpty(session.FullName) ? session.FullName : session.Username;
            UserRoleDescription = session.Role switch
            {
                "Admin" => "Quản trị viên",
                "Manager" => "Quản lý",
                "WarehouseStaff" => "Thủ kho",
                _ => "Nhân viên bán hàng"
            };

            var nameParts = UserDisplayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            UserAvatarInitials = nameParts.Length > 1
                ? $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpper()
                : (UserDisplayName.Length >= 2 ? UserDisplayName[..2].ToUpper() : "TS");
        }
        else
        {
            IsAuthenticated = false;
            UserDisplayName = "Tài khoản phát triển";
            UserRoleDescription = "Chưa đăng nhập";
            UserAvatarInitials = "TS";
        }
    }
}
