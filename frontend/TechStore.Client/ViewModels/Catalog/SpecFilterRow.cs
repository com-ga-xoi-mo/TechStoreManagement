using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>One "key = value" condition of the list spec filter, picked from values that exist in stored products.</summary>
public sealed partial class SpecFilterRow : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<SpecKeyOption> keyOptions = [];
    [ObservableProperty] private string? selectedKey;
    [ObservableProperty] private IReadOnlyList<SpecValueOption> valueOptions = [];
    [ObservableProperty] private string? selectedFilterValue;

    public bool HasKey => !string.IsNullOrEmpty(SelectedKey);

    /// <summary>Replaces the options and keeps the chosen key and value (a ComboBox may clear them while its items change).</summary>
    public void SetOptions(IReadOnlyList<SpecKeyOption> options)
    {
        var key = SelectedKey;
        var value = SelectedFilterValue;
        KeyOptions = options;
        SelectedKey = key;
        ValueOptions = FindValues(key);
        SelectedFilterValue = value;
    }

    partial void OnSelectedKeyChanged(string? value)
    {
        OnPropertyChanged(nameof(HasKey));
        ValueOptions = FindValues(value);
        if (SelectedFilterValue is not null && ValueOptions.Count > 0
            && ValueOptions.All(option => option.FilterValue != SelectedFilterValue))
            SelectedFilterValue = null;
    }

    private IReadOnlyList<SpecValueOption> FindValues(string? key) =>
        KeyOptions.FirstOrDefault(option => option.Key == key)?.Values ?? [];
}
