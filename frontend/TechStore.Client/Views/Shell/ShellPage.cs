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
        this.Background(ThemeResource.Get<Brush>(AppColors.Canvas));

        ShellNavigationView = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            OpenPaneLength = 302,
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

        ShellNavigationView.Resources["NavigationViewItemMargin"] = new Thickness(0, 4, 0, 4);

        // Pane Header: TechStore Logo & Title
        var brandRow = new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
            new Border().Width(42).Height(42).CornerRadius(new CornerRadius(10))
                .Background(ThemeResource.Get<Brush>(AppColors.Primary))
                .Child(new FontIcon().Glyph("\uE7F8").FontSize(20)
                    .Foreground(ThemeResource.Get<Brush>(AppColors.OnPrimary))
                    .HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center)),
            new StackPanel().VerticalAlignment(VerticalAlignment.Center).Spacing(1).Children(
                new TextBlock().Text("TechStore").FontSize(18)
                    .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                    .Foreground(ThemeResource.Get<Brush>(AppColors.Text)),
                new TextBlock().Text("Quản lý bán lẻ").FontSize(12)
                    .Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText))));
        ShellNavigationView.PaneHeader = new StackPanel().Spacing(24)
            .Padding(new Thickness(20, 18, 16, 18)).Children(
                brandRow,
                new TextBlock().Text("MENU CHÍNH").FontSize(11)
                    .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                    .Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText)));

        // Menu Items
        ShellNavigationView.MenuItems.Add(CreateNavItem("Tổng quan", "Dashboard", Symbol.Home));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Sản phẩm", "Products", Symbol.Shop));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Danh mục và Kho", "Inventory", Symbol.AllApps));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Đơn hàng", "Orders", Symbol.Document));
        ShellNavigationView.MenuItems.Add(CreateNavItem("Khách hàng", "Customers", Symbol.Contact));

        // Footer Items
        ShellNavigationView.FooterMenuItems.Add(CreateNavItem("Cài đặt", "Settings", Symbol.Setting));

        // Pane Footer: User Session Profile
        var logoutButton = new Button().Width(34).Height(34).Padding(new Thickness(6))
            .Background(ThemeResource.Get<Brush>(AppColors.Surface))
            .Content(new FontIcon().Glyph("\uE8AC").FontSize(13)
                .Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText)));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(logoutButton, "Đăng xuất tài khoản");
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
            .ColumnSpacing(8)
            .ColumnDefinitions(
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto)
            )
            .Children(
                new Border()
                    .Width(36)
                    .Height(36)
                    .CornerRadius(new CornerRadius(18))
                    .Background(ThemeResource.Get<Brush>(AppColors.InfoFill))
                    .Child(
                        new TextBlock()
                            .FontSize(12)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Foreground(ThemeResource.Get<Brush>(AppColors.Text))
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
                            .Foreground(ThemeResource.Get<Brush>(AppColors.Text))
                            .Text(x => x.Binding(() => ViewModel.UserDisplayName)),
                        new TextBlock()
                            .FontSize(11)
                            .Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText))
                            .Text(x => x.Binding(() => ViewModel.UserRoleDescription))
                    )
                    .Grid(column: 1),
                logoutButton.Grid(column: 2)
            );

        ShellNavigationView.PaneFooter = new Border()
            .BorderBrush(ThemeResource.Get<Brush>(AppColors.Stroke))
            .BorderThickness(new Thickness(0, 1, 0, 0))
            .Padding(new Thickness(14, 12, 14, 12))
            .Child(userInfoRow);

        // Main Content Area
        var errorText = new TextBlock()
            .FontSize(12)
            .Foreground(ThemeResource.Get<Brush>(AppColors.DangerText))
            .Text(x => x.Binding(() => ViewModel.NavigationError))
            .Visibility(x => x.Binding(() => ViewModel.HasNavigationError).Convert((bool hasErr) => hasErr ? Visibility.Visible : Visibility.Collapsed));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(errorText, AutomationLiveSetting.Polite);

        PageHost = new ContentControl()
            .IsTabStop(true)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PageHost, "Nội dung trang");

        ShellNavigationView.Content = new Grid()
            .Padding(new Thickness(24))
            .RowSpacing(8)
            .RowDefinitions(
                new RowDefinition().Height(GridLength.Auto),
                new RowDefinition().Height(new GridLength(1, GridUnitType.Star))
            )
            .Children(
                errorText.Grid(row: 0),
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
