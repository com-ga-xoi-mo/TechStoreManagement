using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;
using TechStore.Client.Services.Theme;
using TechStore.Client.ViewModels.Shell;

namespace TechStore.Client.Views.Shell;

public sealed partial class ShellPage : Page
{
    private readonly ShellNavigationAdapter _navigationAdapter;
    private readonly ContentDialogPresenter _dialogPresenter;
    private readonly IThemePreferenceService _themePreference;
    private readonly ILogger<ShellPage> _logger;
    private bool _closed;

    public ShellViewModel ViewModel { get; }

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
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
