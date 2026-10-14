namespace TechStore.Client.Services.Theme;

public interface IThemePreferenceService
{
    ThemePreference Preference { get; set; }
    event EventHandler<ThemePreference>? PreferenceChanged;
}
