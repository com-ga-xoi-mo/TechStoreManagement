using FluentAssertions;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Theme;
using TechStore.Client.ViewModels.Settings;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.Settings;

public class SettingsViewModelTests
{
    private sealed class FakeThemePreferenceService : IThemePreferenceService
    {
        public ThemePreference Preference { get; set; } = ThemePreference.System;
        public event EventHandler<ThemePreference>? PreferenceChanged;

        public void SetPreference(ThemePreference preference)
        {
            Preference = preference;
            PreferenceChanged?.Invoke(this, preference);
        }
    }

    private sealed class FakeStoreSettingsRepository : IStoreSettingsRepository
    {
        public StoreSettingsDto Settings { get; set; } = new()
        {
            StoreName = "Initial Store",
            Address = "123 Initial Street",
            Phone = "0123456789",
            Email = "initial@store.vn",
            TaxId = "0987654321"
        };

        public UpdateStoreSettingsRequest? LastUpdateRequest { get; private set; }

        public Task<StoreSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Settings);
        }

        public Task<StoreSettingsDto> UpdateSettingsAsync(UpdateStoreSettingsRequest request, CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            Settings = new StoreSettingsDto
            {
                StoreName = request.StoreName,
                Address = request.Address,
                Phone = request.Phone,
                Email = request.Email,
                TaxId = request.TaxId,
                Description = request.Description
            };
            return Task.FromResult(Settings);
        }
    }

    [Fact]
    public async Task LoadContentAsync_PopulatesStoreSettings()
    {
        // Arrange
        var fakeTheme = new FakeThemePreferenceService();
        var fakeRepo = new FakeStoreSettingsRepository();
        var vm = new SettingsViewModel(fakeTheme, fakeRepo);

        // Act
        await vm.ActivateAsync();

        // Assert
        vm.StoreName.Should().Be("Initial Store");
        vm.StoreAddress.Should().Be("123 Initial Street");
        vm.StorePhone.Should().Be("0123456789");
    }

    [Fact]
    public async Task SaveStoreSettingsAsync_UpdatesRepositoryAndShowsSuccess()
    {
        // Arrange
        var fakeTheme = new FakeThemePreferenceService();
        var fakeRepo = new FakeStoreSettingsRepository();
        var vm = new SettingsViewModel(fakeTheme, fakeRepo)
        {
            StoreName = "New Flagship Store",
            StoreAddress = "456 New Street",
            StorePhone = "0999888777"
        };

        // Act
        await vm.SaveStoreSettingsCommand.ExecuteAsync(null);

        // Assert
        vm.ShowSaveSuccess.Should().BeTrue();
        vm.SaveErrorMessage.Should().BeEmpty();
        fakeRepo.LastUpdateRequest.Should().NotBeNull();
        fakeRepo.LastUpdateRequest!.StoreName.Should().Be("New Flagship Store");
        fakeRepo.LastUpdateRequest.Address.Should().Be("456 New Street");
    }

    [Fact]
    public void ThemeSelection_UpdatesThemeService()
    {
        // Arrange
        var fakeTheme = new FakeThemePreferenceService();
        var vm = new SettingsViewModel(fakeTheme);

        // Act
        vm.SelectedThemeIndex = (int)ThemePreference.Dark;

        // Assert
        fakeTheme.Preference.Should().Be(ThemePreference.Dark);
    }
}
