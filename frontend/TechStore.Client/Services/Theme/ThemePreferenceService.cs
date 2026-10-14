namespace TechStore.Client.Services.Theme;

public sealed class ThemePreferenceService : IThemePreferenceService
{
    private ThemePreference _preference;

    public ThemePreference Preference
    {
        get => _preference;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_preference == value) return;
            _preference = value;
            PreferenceChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<ThemePreference>? PreferenceChanged;
}
