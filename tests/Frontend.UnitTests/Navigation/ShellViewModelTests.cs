using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.Services.Theme;
using TechStore.Client.ViewModels.Settings;
using TechStore.Client.ViewModels.Shell;

namespace TechStore.Client.UnitTests.Navigation;

public sealed class ShellViewModelTests
{
    [Fact]
    public void NavigationSuccess_UpdatesSelectedRouteAndTitle()
    {
        var navigation = new NavigationService(new DestinationPreparer());
        var session = new InMemorySessionStore();
        var viewModel = new ShellViewModel(navigation, session);

        viewModel.NavigateCommand.Execute(ShellDestination.Products);

        Assert.Equal(ShellDestination.Products, viewModel.SelectedDestination);
        Assert.Equal("Sản phẩm", viewModel.CurrentTitle);
        Assert.Empty(viewModel.NavigationError);
        Assert.Equal(5, viewModel.MainDestinations.Count);
        Assert.Equal(ShellDestination.Settings, viewModel.SettingsDestination.Id);
    }

    [Fact]
    public void NavigationFailure_PreservesTitleAndSelectionAndUsesSafeError()
    {
        var navigation = new NavigationService(new DestinationPreparer(allow: false));
        var session = new InMemorySessionStore();
        var viewModel = new ShellViewModel(navigation, session);

        viewModel.NavigateCommand.Execute(ShellDestination.Products);

        Assert.Equal(ShellDestination.Dashboard, viewModel.SelectedDestination);
        Assert.Equal("Tổng quan", viewModel.CurrentTitle);
        Assert.Equal("Không thể mở mục này. Hãy thử lại.", viewModel.NavigationError);
    }

    [Fact]
    public void SettingsTheme_ChangesWithinSessionAndDefaultsToSystemForNewService()
    {
        var theme = new ThemePreferenceService();
        using var viewModel = new SettingsViewModel(theme);

        viewModel.SelectedThemeIndex = (int)ThemePreference.Dark;

        Assert.Equal(ThemePreference.Dark, theme.Preference);
        Assert.Equal((int)ThemePreference.Dark, viewModel.SelectedThemeIndex);
        Assert.Equal(ThemePreference.System, new ThemePreferenceService().Preference);
    }

    private sealed class DestinationPreparer(bool allow = true) : IDestinationPreparer
    {
        public bool Prepare(AppRoute route) => allow;
    }
}
