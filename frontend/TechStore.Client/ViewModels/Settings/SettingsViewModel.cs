using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Theme;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Settings;

public sealed partial class SettingsViewModel : PageViewModel, IDisposable
{
    private readonly IThemePreferenceService _theme;
    private readonly IStoreSettingsRepository? _storeSettingsRepository;

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

    [ObservableProperty] private string storeName = "TechStore - Hệ thống Bán lẻ Công nghệ & Phụ kiện";
    [ObservableProperty] private string storeAddress = "123 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh";
    [ObservableProperty] private string storePhone = "028 3822 6868";
    [ObservableProperty] private string storeEmail = "contact@techstore.vn";
    [ObservableProperty] private string storeTaxId = "0315892410";
    [ObservableProperty] private string storeDescription = string.Empty;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool showSaveSuccess;
    [ObservableProperty] private string saveErrorMessage = string.Empty;

    public string ApplicationName => "TechStore Management";
    public string ApplicationVersion => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "—";
    public string RuntimeDescription => ".NET 10 · Uno Platform 6.7.30";

    public SettingsViewModel(IThemePreferenceService theme, IStoreSettingsRepository? storeSettingsRepository = null)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _storeSettingsRepository = storeSettingsRepository;
        _selectedThemeIndex = (int)theme.Preference;
        _theme.PreferenceChanged += OnPreferenceChanged;
    }

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        if (_storeSettingsRepository != null)
        {
            try
            {
                var settings = await _storeSettingsRepository.GetSettingsAsync(cancellationToken);
                if (settings != null)
                {
                    StoreName = settings.StoreName;
                    StoreAddress = settings.Address;
                    StorePhone = settings.Phone;
                    StoreEmail = settings.Email;
                    StoreTaxId = settings.TaxId;
                    StoreDescription = settings.Description;
                }
            }
            catch
            {
                // Fallback to default values for offline/demo operation
            }
        }
        return true;
    }

    [RelayCommand]
    public async Task SaveStoreSettingsAsync()
    {
        if (_storeSettingsRepository == null)
        {
            ShowSaveSuccess = true;
            return;
        }

        SaveErrorMessage = string.Empty;
        ShowSaveSuccess = false;
        IsSaving = true;

        try
        {
            var request = new UpdateStoreSettingsRequest
            {
                StoreName = StoreName,
                Address = StoreAddress,
                Phone = StorePhone,
                Email = StoreEmail,
                TaxId = StoreTaxId,
                Description = StoreDescription
            };

            await _storeSettingsRepository.UpdateSettingsAsync(request, CancellationToken.None);
            ShowSaveSuccess = true;
        }
        catch (Exception ex)
        {
            SaveErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void Dispose() => _theme.PreferenceChanged -= OnPreferenceChanged;

    private void OnPreferenceChanged(object? sender, ThemePreference preference) =>
        SelectedThemeIndex = (int)preference;
}
