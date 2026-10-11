using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TechStore.Client.Services.Navigation;

namespace TechStore.Client.ViewModels.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly Services.Session.ICurrentSession _session;
    private readonly Services.Dialog.IDialogService _dialogService;
    private readonly IRootNavigationService _rootNavigation;

    [ObservableProperty]
    private RouteId currentRoute;

    [ObservableProperty]
    private string footerText = string.Empty;

    [ObservableProperty]
    private bool isSignOutVisible;

    public ObservableCollection<RouteMetadata> MenuItems { get; } = new();

    public ShellViewModel(
        INavigationService navigationService, 
        Services.Session.ICurrentSession session,
        Services.Dialog.IDialogService dialogService,
        IRootNavigationService rootNavigation)
    {
        _navigationService = navigationService;
        _session = session;
        _dialogService = dialogService;
        _rootNavigation = rootNavigation;
        
        UpdateFooterText();

        // Listen for session changes
        _session.SessionChanged += (s, snap) => UpdateFooterText();

        // Populate allowed menu items
        RefreshMenuItems();

        CurrentRoute = _navigationService.CurrentRoute;
        
        // Listen for internal route changes (e.g. Back button)
        _navigationService.RouteChanged += (s, newRoute) => CurrentRoute = newRoute;
    }

    private void UpdateFooterText()
    {
        FooterText = _session.Current.Status switch 
        {
            Services.Session.SessionStatus.Preview => "Preview Mode (Local UI Demo)",
            Services.Session.SessionStatus.Authenticated => $"Logged in as {_session.Current.DisplayName}",
            _ => "Not logged in"
        };
        IsSignOutVisible = _session.Current.Status != Services.Session.SessionStatus.Unknown;
    }

    [RelayCommand]
    private void SignOut()
    {
        _session.ClearSession();
        _navigationService.Reset();
        
        _rootNavigation.ShowLogin();
    }

    private void RefreshMenuItems()
    {
        MenuItems.Clear();
        foreach (var route in _navigationService.GetAllowedMenuRoutes())
        {
            MenuItems.Add(route);
        }
    }

    // Called when the user clicks a menu item
    public void Navigate(RouteId routeId)
    {
        _navigationService.NavigateTo(routeId);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ShowSettingsDialogAsync()
    {
        await _dialogService.ShowMessageAsync(
            "Cài Đặt Hệ Thống", 
            "Màn hình cài đặt hiện chưa được triển khai.\nTrong tương lai, phần này sẽ hiển thị thông tin máy chủ và cấu hình.", 
            "Đóng");
    }
}
