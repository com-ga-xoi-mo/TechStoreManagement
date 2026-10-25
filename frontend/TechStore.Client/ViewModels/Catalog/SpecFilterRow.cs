using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>One "key = value" condition of the list spec filter, picked from values that exist in stored products.</summary>
public sealed partial class SpecFilterRow : ObservableObject
{
    private IReadOnlyList<SpecKeyOption> _options = [];

    [ObservableProperty] private IReadOnlyList<Choice> keyChoices = [];
    [ObservableProperty] private string? selectedKey;
    [ObservableProperty] private IReadOnlyList<Choice> valueChoices = [];
    [ObservableProperty] private string? selectedFilterValue;

    public bool HasKey => !string.IsNullOrEmpty(SelectedKey);

    /// <summary>Replaces the options and keeps the chosen key and value (a ComboBox may clear them while its items change).</summary>
    public void SetOptions(IReadOnlyList<SpecKeyOption> options)
    {
        var key = SelectedKey;
        var value = SelectedFilterValue;
        _options = options;
        KeyChoices = options
            .Select(o => new Choice(o.Key, SpecPresentation.WithCount(SpecPresentation.KeyName(o.Key, o.Label), o.ProductCount)))
            .ToList();
        SelectedKey = key;
        ValueChoices = BuildValueChoices(key);
        SelectedFilterValue = value;
    }

    partial void OnSelectedKeyChanged(string? value)
    {
        OnPropertyChanged(nameof(HasKey));
        ValueChoices = BuildValueChoices(value);
        if (SelectedFilterValue is not null && ValueChoices.Count > 0
            && ValueChoices.All(choice => choice.Value != SelectedFilterValue))
            SelectedFilterValue = null;
    }

    private IReadOnlyList<Choice> BuildValueChoices(string? key)
    {
        var option = _options.FirstOrDefault(o => o.Key == key);
        if (option is null) return [];
        return option.Values
            .Select(v => new Choice(v.FilterValue, SpecPresentation.WithCount(SpecPresentation.ValueText(v.Label, v.Kind, option.Unit), v.ProductCount)))
            .ToList();
    }
}
