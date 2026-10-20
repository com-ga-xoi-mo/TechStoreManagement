using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.Services.Theme;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Shell;
using Uno.Extensions.Markup;
using Windows.UI;

namespace TechStore.Client.Views.Shell;

public sealed class ShellPage : Page
{
    private readonly ShellNavigationAdapter _navigationAdapter;
    private readonly ContentDialogPresenter _dialogPresenter;
    private readonly IThemePreferenceService _themePreference;
    private readonly ILogger<ShellPage> _logger;
    private bool _closed;

    public ShellViewModel ViewModel { get; }
    public NavigationView ShellNavigationView { get; private set; } = null!;
    public ContentControl PageHost { get; private set; } = null!;

    public ShellPage(
        ShellViewModel viewModel,
        ShellNavigationAdapter navigationAdapter,
        ContentDialogPresenter dialogPresenter,
        IThemePreferenceService themePreference,
        ILogger<ShellPage> logger)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        _navigationAdapter = navigationAdapter;
        _dialogPresenter = dialogPresenter;
        _themePreference = themePreference;
        _logger = logger;

        BuildUi();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnShellSizeChanged;
    }

    private void BuildUi()
    {
        this.Background(new SolidColorBrush(StitchTokens.SurfaceCanvas));

        ShellNavigationView = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            OpenPaneLength = 250,
            CompactPaneLength = 64,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
            CompactModeThresholdWidth = 800,
            ExpandedModeThresholdWidth = 1100,
            SelectionFollowsFocus = NavigationViewSelectionFollowsFocus.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        ShellNavigationView.Resources["NavigationViewItemMargin"] = new Thickness(0, 4, 0, 4);

        // Pane Header: TechStore Logo & Title
        ShellNavigationView.PaneHeader = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(12)
            .Padding(new Thickness(16, 12, 12, 16))
            .Children(
                new Border()
                    .Width(36)
                    .Height(36)
                    .CornerRadius(new CornerRadius(10))
                    .Background(new SolidColorBrush(StitchTokens.Primary))
                    .Child(
                        new TextBlock()
                            .Text("T")
                            .Foreground(new SolidColorBrush(Colors.White))
                            .FontSize(20)
                            .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                    ),
                new StackPanel()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Spacing(1)
                    .Children(
                        new TextBlock()
                            .Text("TechStore")
                            .FontSize(16)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentTitle)),
                        new TextBlock()
                            .Text("QUẢN TRỊ HỆ THỐNG")
                            .FontSize(11)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentMuted))
                    )
            );

        // Menu Items
        ShellNavigationView.MenuItems.Add(CreateNavItem("Tổng quan", "Dashboard", Symbol.Home));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Sản phẩm", "Products", Symbol.Shop));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Danh mục và Kho", "Inventory", Symbol.AllApps));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Đơn hàng", "Orders", Symbol.Document));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Khách hàng", "Customers", Symbol.Contact));

        // Footer Items
        ShellNavigationView.FooterMenuItems.Add(CreateNavItem("Cài đặt", "Settings", Symbol.Setting));

        // Pane Footer: User Session Profile
        var logoutButton = new Button()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Background(new SolidColorBrush(StitchTokens.ErrorBg))
            .BorderBrush(new SolidColorBrush(StitchTokens.ErrorBorder))
            .BorderThickness(new Thickness(1))
            .Padding(new Thickness(12, 6, 12, 6))
            .CornerRadius(new CornerRadius(8))
            .Margin(new Thickness(0, 8, 0, 0))
            .Content(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Spacing(8)
                    .Children(
                        new FontIcon()
                            .Glyph("\uE8AC")
                            .FontSize(12)
                            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed)),
                        new TextBlock()
                            .Text("Đăng xuất tài khoản")
                            .FontSize(12)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed))
                    )
            );
        ToolTipService.SetToolTip(logoutButton, "Đăng xuất khỏi hệ thống");
        logoutButton.Click += (s, e) =>
        {
            if (Application.Current is App app && app.Services != null)
            {
                var sessionStore = (ISessionStore?)app.Services.GetService(typeof(ISessionStore));
                sessionStore?.Clear();
            }
        };

        var userInfoRow = new Grid()
            .ColumnSpacing(10)
            .ColumnDefinitions(
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star))
            )
            .Children(
                new Border()
                    .Width(36)
                    .Height(36)
                    .CornerRadius(new CornerRadius(18))
                    .Background(new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)))
                    .Child(
                        new TextBlock()
                            .FontSize(12)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentTitle))
                            .Text(x => x.Binding(() => ViewModel.UserAvatarInitials))
                    )
                    .Grid(column: 0),
                new StackPanel()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Spacing(2)
                    .Children(
                        new TextBlock()
                            .FontSize(13)
                            .FontWeight(Microsoft.UI.Text.FontWeights.Medium)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentBody))
                            .Text(x => x.Binding(() => ViewModel.UserDisplayName)),
                        new TextBlock()
                            .FontSize(11)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentMuted))
                            .Text(x => x.Binding(() => ViewModel.UserRoleDescription))
                    )
                    .Grid(column: 1)
            );

        ShellNavigationView.PaneFooter = new Border()
            .BorderBrush(new SolidColorBrush(StitchTokens.SurfaceBorder))
            .BorderThickness(new Thickness(0, 1, 0, 0))
            .Padding(new Thickness(14, 12, 14, 12))
            .Child(
                new StackPanel()
                    .Spacing(4)
                    .Children(userInfoRow, logoutButton)
            );

        // Main Content Area
        var errorText = new TextBlock()
            .FontSize(12)
            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed))
            .Text(x => x.Binding(() => ViewModel.NavigationError))
            .Visibility(x => x.Binding(() => ViewModel.HasNavigationError).Convert((bool hasErr) => hasErr ? Visibility.Visible : Visibility.Collapsed));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(errorText, AutomationLiveSetting.Polite);

        var topStack = new StackPanel()
            .Spacing(8)
            .Children(
                new Grid()
                    .ColumnSpacing(16)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto)
                    )
                    .Children(
                        new StackPanel()
                            .Spacing(3)
                            .Children(
                                new TextBlock()
                                    .FontSize(20)
                                    .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                                    .Foreground(new SolidColorBrush(StitchTokens.ContentTitle))
                                    .Text(x => x.Binding(() => ViewModel.CurrentTitle)),
                                new TextBlock()
                                    .Text("Không gian quản trị TechStore")
                                    .FontSize(12)
                                    .Foreground(new SolidColorBrush(StitchTokens.ContentMuted))
                            )
                            .Grid(column: 0),
                        new Border()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .CornerRadius(new CornerRadius(16))
                            .Padding(new Thickness(12, 6, 12, 6))
                            .Background(new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xF3, 0xC7)))
                            .BorderBrush(new SolidColorBrush(Color.FromArgb(0xFF, 0xFB, 0xBF, 0x24)))
                            .BorderThickness(new Thickness(1))
                            .Child(
                                new TextBlock()
                                    .Text("BẢN PHÁT TRIỂN · TECHSTORE")
                                    .FontSize(11)
                                    .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                                    .Foreground(new SolidColorBrush(Color.FromArgb(0xFF, 0x92, 0x40, 0x0E)))
                            )
                            .Grid(column: 1)
                    ),
                errorText
            );

        PageHost = new ContentControl()
            .IsTabStop(true)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PageHost, "Nội dung trang");

        ShellNavigationView.Content = new Grid()
            .Padding(new Thickness(24, 16, 24, 16))
            .RowSpacing(12)
            .RowDefinitions(
                new RowDefinition().Height(GridLength.Auto),
                new RowDefinition().Height(new GridLength(1, GridUnitType.Star))
            )
            .Children(
                topStack.Grid(row: 0),
                PageHost.Grid(row: 1)
            );

        this.Content(ShellNavigationView);
    }

    private static NavigationViewItem CreateNavItem(string content, string tag, Symbol symbol)
    {
        var item = new NavigationViewItem()
            .Content(content)
            .Icon(new SymbolIcon(symbol));
        item.Tag = tag;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, content);
        return item;
    }

    public static Visibility VisibilityFor(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    public void CloseShell()
    {
        if (_closed) return;
        _closed = true;
        _themePreference.PreferenceChanged -= OnThemePreferenceChanged;
        _dialogPresenter.Detach();
        _navigationAdapter.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _themePreference.PreferenceChanged -= OnThemePreferenceChanged;
        _themePreference.PreferenceChanged += OnThemePreferenceChanged;
        ApplyTheme();
        _navigationAdapter.Attach(ShellNavigationView, PageHost, ViewModel);
        _dialogPresenter.Attach(this, _navigationAdapter.FocusCurrentPage);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => CloseShell();

    private void OnThemePreferenceChanged(object? sender, ThemePreference preference) => ApplyTheme();

    private void OnShellSizeChanged(object sender, SizeChangedEventArgs args) =>
        _logger.LogInformation(
            "Shell viewport {Width:0}×{Height:0} DIP at raster scale {Scale:0.##}.",
            args.NewSize.Width,
            args.NewSize.Height,
            XamlRoot?.RasterizationScale ?? 1);

    private void ApplyTheme() => RequestedTheme = _themePreference.Preference switch
    {
        ThemePreference.Light => ElementTheme.Light,
        ThemePreference.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };
}
