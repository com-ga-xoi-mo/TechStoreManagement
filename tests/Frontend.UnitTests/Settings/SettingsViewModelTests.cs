using TechStore.Client.Services.Theme;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Settings;

namespace TechStore.Client.UnitTests.Settings;

public sealed class SettingsViewModelTests
{
    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void SelectingTheme_UpdatesSessionPreference(ThemePreference preference)
    {
        var service = new ThemePreferenceService();
        using var viewModel = new SettingsViewModel(service);

        viewModel.SelectedThemeIndex = (int)preference;

        Assert.Equal(preference, service.Preference);
        Assert.Equal((int)preference, viewModel.SelectedThemeIndex);
    }

    [Fact]
    public void PreferenceChangedExternally_UpdatesViewModelUntilDisposed()
    {
        var service = new ThemePreferenceService();
        var viewModel = new SettingsViewModel(service);

        service.Preference = ThemePreference.Dark;
        Assert.Equal((int)ThemePreference.Dark, viewModel.SelectedThemeIndex);

        viewModel.Dispose();
        service.Preference = ThemePreference.Light;

        Assert.Equal((int)ThemePreference.Dark, viewModel.SelectedThemeIndex);
    }

    [Fact]
    public async Task Activate_ExposesSettingsWithoutBackendData()
    {
        using var viewModel = new SettingsViewModel(new ThemePreferenceService());

        await viewModel.ActivateAsync();

        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.Equal("Theo hệ thống", viewModel.ThemeOptions[viewModel.SelectedThemeIndex]);
        Assert.Equal("TechStore Management", viewModel.ApplicationName);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ApplicationVersion));
    }

    [Fact]
    public async Task ChangingThemeWhileSettingsIsOpen_DoesNotReloadOrClearContent()
    {
        var theme = new ThemePreferenceService();
        using var viewModel = new SettingsViewModel(theme);
        await viewModel.ActivateAsync();

        viewModel.SelectedThemeIndex = (int)ThemePreference.Dark;

        Assert.Equal(ThemePreference.Dark, theme.Preference);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public void NewSettingsActivation_UsesPreferenceFromCurrentAppSession()
    {
        var theme = new ThemePreferenceService();
        using (var firstActivation = new SettingsViewModel(theme))
            firstActivation.SelectedThemeIndex = (int)ThemePreference.Dark;

        using var revisit = new SettingsViewModel(theme);

        Assert.Equal((int)ThemePreference.Dark, revisit.SelectedThemeIndex);
        Assert.Equal(ThemePreference.System, new ThemePreferenceService().Preference);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void SelectingUnknownTheme_LeavesSessionPreferenceUnchanged(int index)
    {
        var service = new ThemePreferenceService();
        using var viewModel = new SettingsViewModel(service);

        viewModel.SelectedThemeIndex = index;

        Assert.Equal(ThemePreference.System, service.Preference);
        Assert.Equal((int)ThemePreference.System, viewModel.SelectedThemeIndex);
    }
}
