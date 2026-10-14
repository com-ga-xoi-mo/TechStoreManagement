using TechStore.Client.Services.Theme;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Settings;

public sealed partial class SettingsViewModel : PageViewModel, IDisposable
{
    private readonly IThemePreferenceService _theme;

    public string[] ThemeOptions { get; } = ["Theo hệ thống", "Sáng", "Tối"];

    private int _selectedThemeIndex;

    public int SelectedThemeIndex
    {
        get => _selectedThemeIndex;
        set
        {
            if (value < 0 || value >= ThemeOptions.Length || !SetProperty(ref _selectedThemeIndex, value))
                return;

            var preference = (ThemePreference)value;
            if (_theme.Preference != preference)
                _theme.Preference = preference;
        }
    }

    public string ApplicationName => "TechStore Management";
    public string ApplicationVersion => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "—";
    public string RuntimeDescription => ".NET 10 · Uno Platform 6.7.30";

    public SettingsViewModel(IThemePreferenceService theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _selectedThemeIndex = (int)theme.Preference;
        _theme.PreferenceChanged += OnPreferenceChanged;
    }

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public void Dispose() => _theme.PreferenceChanged -= OnPreferenceChanged;

    private void OnPreferenceChanged(object? sender, ThemePreference preference) =>
        SelectedThemeIndex = (int)preference;
}
