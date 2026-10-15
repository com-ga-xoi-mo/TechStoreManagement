using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.Logging;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.Services.Theme;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Shell;
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
        Background = new SolidColorBrush(StitchTokens.SurfaceCanvas);

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
        ShellNavigationView.ItemInvoked += (s, e) =>
        {
            if (e.InvokedItemContainer is NavigationViewItem item && item.Tag is string route)
            {
                if (ShellDestinations.TryGet(route, out var dest))
                {
                    ViewModel.NavigateCommand.Execute(dest.Id);
                }
            }
        };

        // Align menu items to spread gracefully
        ShellNavigationView.Resources["NavigationViewItemMargin"] = new Thickness(0, 4, 0, 4);

        // Pane Header: TechStore Logo & Title
        var logoIcon = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(StitchTokens.Primary),
            Child = new TextBlock
            {
                Text = "T",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var titleBlock = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = "TechStore",
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(StitchTokens.ContentTitle)
                },
                new TextBlock
                {
                    Text = "QUẢN TRỊ HỆ THỐNG",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(StitchTokens.ContentMuted)
                }
            }
        };

        ShellNavigationView.PaneHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Padding = new Thickness(16, 12, 12, 16),
            Children = { logoIcon, titleBlock }
        };

        // Menu Items
        ShellNavigationView.MenuItems.Add(CreateNavItem("Tổng quan", "Dashboard", Symbol.Home));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Sản phẩm", "Products", Symbol.Shop));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Danh mục và Kho", "Inventory", Symbol.AllApps));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Đơn hàng", "Orders", Symbol.Document));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Khách hàng", "Customers", Symbol.Contact));

        // Footer Items
        ShellNavigationView.FooterMenuItems.Add(CreateNavItem("Cài đặt", "Settings", Symbol.Setting));

        // Pane Footer: User Session Profile
        var avatarBorder = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)),
            Child = new TextBlock
            {
                Text = ViewModel.UserAvatarInitials,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(StitchTokens.ContentTitle)
            }
        };

        var userNameText = new TextBlock
        {
            Text = ViewModel.UserDisplayName,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = new SolidColorBrush(StitchTokens.ContentBody)
        };

        var userRoleText = new TextBlock
        {
            Text = ViewModel.UserRoleDescription,
            FontSize = 11,
            Foreground = new SolidColorBrush(StitchTokens.ContentMuted)
        };

        var logoutStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Children =
            {
                new FontIcon { Glyph = "\uE8AC", FontSize = 12, Foreground = new SolidColorBrush(StitchTokens.ErrorRed) },
                new TextBlock { Text = "Đăng xuất tài khoản", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(StitchTokens.ErrorRed) }
            }
        };

        var logoutButton = new Button
        {
            Content = logoutStack,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(StitchTokens.ErrorBg),
            BorderBrush = new SolidColorBrush(StitchTokens.ErrorBorder),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(logoutButton, "Đăng xuất khỏi hệ thống");
        logoutButton.Click += (s, e) =>
        {
            if (Application.Current is App app && app.Services != null)
            {
                var sessionStore = (ISessionStore?)app.Services.GetService(typeof(ISessionStore));
                sessionStore?.Clear();
            }
        };

        var userTextStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
            Children = { userNameText, userRoleText }
        };

        // Top row: Avatar + Name & Role
        var userInfoRow = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            }
        };
        Grid.SetColumn(avatarBorder, 0);
        Grid.SetColumn(userTextStack, 1);
        userInfoRow.Children.Add(avatarBorder);
        userInfoRow.Children.Add(userTextStack);

        // Container Stack: Dòng thông tin người dùng ở trên, nút Đăng xuất full-width ở hàng dưới
        var paneFooterContainer = new StackPanel
        {
            Spacing = 4,
            Children = { userInfoRow, logoutButton }
        };

        ShellNavigationView.PaneFooter = new Border
        {
            BorderBrush = new SolidColorBrush(StitchTokens.SurfaceBorder),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 12, 14, 12),
            Child = paneFooterContainer
        };

        // Listen for session updates
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.UserDisplayName))
                userNameText.Text = ViewModel.UserDisplayName;
            if (e.PropertyName == nameof(ViewModel.UserRoleDescription))
                userRoleText.Text = ViewModel.UserRoleDescription;
            if (e.PropertyName == nameof(ViewModel.UserAvatarInitials))
                ((TextBlock)avatarBorder.Child).Text = ViewModel.UserAvatarInitials;
        };

        // Main Content Area
        var titleText = new TextBlock
        {
            Text = ViewModel.CurrentTitle,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(StitchTokens.ContentTitle)
        };

        var subtitleText = new TextBlock
        {
            Text = "Không gian quản trị TechStore",
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ContentMuted)
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.CurrentTitle))
                titleText.Text = ViewModel.CurrentTitle;
        };

        var headerTitleStack = new StackPanel
        {
            Spacing = 3,
            Children = { titleText, subtitleText }
        };

        var badgeBorder = new Border
        {
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 6, 12, 6),
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xF3, 0xC7)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFB, 0xBF, 0x24)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "BẢN PHÁT TRIỂN · TECHSTORE",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x92, 0x40, 0x0E))
            }
        };

        var topHeaderGrid = new Grid
        {
            ColumnSpacing = 16,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        Grid.SetColumn(headerTitleStack, 0);
        Grid.SetColumn(badgeBorder, 1);
        topHeaderGrid.Children.Add(headerTitleStack);
        topHeaderGrid.Children.Add(badgeBorder);

        var errorText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ErrorRed),
            Visibility = Visibility.Collapsed
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(errorText, AutomationLiveSetting.Polite);

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(ViewModel.NavigationError) or nameof(ViewModel.HasNavigationError))
            {
                errorText.Text = ViewModel.NavigationError;
                errorText.Visibility = ViewModel.HasNavigationError ? Visibility.Visible : Visibility.Collapsed;
            }
        };

        var topStack = new StackPanel
        {
            Spacing = 8,
            Children = { topHeaderGrid, errorText }
        };

        PageHost = new ContentControl
        {
            IsTabStop = true,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PageHost, "Nội dung trang");

        var mainContentGrid = new Grid
        {
            Padding = new Thickness(24, 16, 24, 16),
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };
        Grid.SetRow(topStack, 0);
        Grid.SetRow(PageHost, 1);
        mainContentGrid.Children.Add(topStack);
        mainContentGrid.Children.Add(PageHost);

        ShellNavigationView.Content = mainContentGrid;
        Content = ShellNavigationView;
    }

    private static NavigationViewItem CreateNavItem(string content, string tag, Symbol symbol)
    {
        var item = new NavigationViewItem
        {
            Content = content,
            Tag = tag,
            Icon = new SymbolIcon(symbol)
        };
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
