using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>
/// One spec line of the editor. The user types a name; a name that matches a definition applies its key, kind and
/// unit (kind locked), any other name generates a snake_case key. A row loaded with a key keeps that key.
/// </summary>
public sealed partial class SpecRowItem : ObservableObject
{
    private SpecCatalog _catalog = SpecCatalog.Empty;
    private bool _applying;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string key = string.Empty;
    [ObservableProperty] private int kindIndex;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private string? unit;
    [ObservableProperty] private bool isKnown;
    [ObservableProperty] private bool isKeyLocked;
    [ObservableProperty] private bool isKeyEditorOpen;
    [ObservableProperty] private IReadOnlyList<SpecSuggestion> suggestions = [];
    [ObservableProperty] private string? error;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasSuggestions => Suggestions.Count > 0;
    public bool IsKindEditable => !IsKnown;
    public bool HasUnit => !string.IsNullOrWhiteSpace(Unit);
    /// <summary>Only a new spec's generated key may be edited; defined and loaded keys stay as they are.</summary>
    public bool CanEditKey => (!IsKnown && !IsKeyLocked) || IsKeyEditorOpen;

    public string KeyCaption => Key.Length == 0
        ? "Nhập tên để tạo thông số"
        : IsKnown
            ? $"key: {Key}"
            : string.IsNullOrWhiteSpace(Name) ? $"key: {Key} · chưa có tên hiển thị" : $"Thông số mới · key: {Key}";

    /// <summary>Uses <paramref name="catalog"/> from now on and shows the definition name of a loaded key.</summary>
    public void AttachCatalog(SpecCatalog catalog)
    {
        _catalog = catalog;
        if (IsKeyLocked && _catalog.FindByKey(Key) is { } definition)
        {
            Apply(definition, showName: true);
            return;
        }
        Resolve();
    }

    /// <summary>Uses a picked suggestion: its name replaces what was typed.</summary>
    public void Pick(SpecDefinition definition) => Apply(definition, showName: true);

    public SpecDraft ToDraft() => new(Key, KindFromIndex(KindIndex), Value);

    /// <summary>The name to save for a key that has no definition yet, or null.</summary>
    public SpecLabelDraft? ToLabelDraft()
    {
        if (IsKnown || Key.Length == 0) return null;
        var (label, _) = SpecKeyGenerator.SplitUnit(Name);
        return label.Length == 0 ? null : new SpecLabelDraft(Key, label, Unit, KindFromIndex(KindIndex));
    }

    /// <summary>A row for a stored spec: its key stays as loaded.</summary>
    public static SpecRowItem From(SpecDraft d) => new()
    {
        Key = d.Key,
        KindIndex = IndexFromKind(d.Kind),
        Value = d.Value,
        IsKeyLocked = true
    };

    public static SpecValueKind KindFromIndex(int index) => index switch
    {
        1 => SpecValueKind.Number,
        2 => SpecValueKind.Boolean,
        _ => SpecValueKind.Text
    };

    public static int IndexFromKind(SpecValueKind kind) => kind switch
    {
        SpecValueKind.Number => 1,
        SpecValueKind.Boolean => 2,
        _ => 0
    };

    partial void OnNameChanged(string value)
    {
        if (!_applying) Resolve();
    }

    partial void OnKeyChanged(string value)
    {
        if (!_applying && IsKeyEditorOpen) IsKeyLocked = true;
        OnPropertyChanged(nameof(KeyCaption));
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnSuggestionsChanged(IReadOnlyList<SpecSuggestion> value) => OnPropertyChanged(nameof(HasSuggestions));
    partial void OnUnitChanged(string? value) => OnPropertyChanged(nameof(HasUnit));

    partial void OnIsKnownChanged(bool value)
    {
        OnPropertyChanged(nameof(IsKindEditable));
        OnPropertyChanged(nameof(KeyCaption));
        OnPropertyChanged(nameof(CanEditKey));
    }

    partial void OnIsKeyLockedChanged(bool value) => OnPropertyChanged(nameof(CanEditKey));
    partial void OnIsKeyEditorOpenChanged(bool value) => OnPropertyChanged(nameof(CanEditKey));

    private void Resolve()
    {
        var (label, unit) = SpecKeyGenerator.SplitUnit(Name);
        var generated = SpecKeyGenerator.Generate(label, unit);
        var definition = _catalog.FindByName(Name) ?? _catalog.FindByName(label)
            ?? (IsKeyLocked ? _catalog.FindByKey(Key) : _catalog.FindByKey(generated));
        if (definition is not null && (!IsKeyLocked || definition.Key == Key))
        {
            // The typed text stays as typed (replacing it while typing would move the caret).
            Apply(definition, showName: false);
        }
        else
        {
            _applying = true;
            IsKnown = false;
            if (!IsKeyLocked) Key = generated ?? string.Empty;
            Unit = unit;
            _applying = false;
            Suggestions = string.IsNullOrWhiteSpace(Name)
                ? []
                : _catalog.Suggest(Name).Select(d => new SpecSuggestion(this, d)).ToList();
        }
        OnPropertyChanged(nameof(KeyCaption));
    }

    private void Apply(SpecDefinition definition, bool showName)
    {
        _applying = true;
        if (showName) Name = definition.Label;
        Key = definition.Key;
        KindIndex = IndexFromKind(definition.Kind);
        Unit = definition.Unit;
        IsKnown = true;
        Suggestions = [];
        _applying = false;
        OnPropertyChanged(nameof(KeyCaption));
    }
}

public sealed partial class VariantRowItem : ObservableObject
{
    public VariantRowItem(Guid? id)
    {
        Id = id;
    }

    public Guid? Id { get; }
    public bool IsSaved => Id.HasValue;
    public bool IsSkuEditable => !IsSaved;

    [ObservableProperty] private string variantName = string.Empty;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string priceText = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private bool isActive = true;

    [ObservableProperty] private string? nameError;
    [ObservableProperty] private string? skuError;
    [ObservableProperty] private string? priceError;
    [ObservableProperty] private string? barcodeError;
    [ObservableProperty] private string? specsError;

    public bool HasNameError => !string.IsNullOrWhiteSpace(NameError);
    public bool HasSkuError => !string.IsNullOrWhiteSpace(SkuError);
    public bool HasPriceError => !string.IsNullOrWhiteSpace(PriceError);
    public bool HasBarcodeError => !string.IsNullOrWhiteSpace(BarcodeError);
    public bool HasSpecsError => !string.IsNullOrWhiteSpace(SpecsError);

    public ObservableCollection<SpecRowItem> Specs { get; } = [];

    partial void OnNameErrorChanged(string? value) => OnPropertyChanged(nameof(HasNameError));
    partial void OnSkuErrorChanged(string? value) => OnPropertyChanged(nameof(HasSkuError));
    partial void OnPriceErrorChanged(string? value) => OnPropertyChanged(nameof(HasPriceError));
    partial void OnBarcodeErrorChanged(string? value) => OnPropertyChanged(nameof(HasBarcodeError));
    partial void OnSpecsErrorChanged(string? value) => OnPropertyChanged(nameof(HasSpecsError));

    public VariantDraft ToDraft() =>
        new(Id, VariantName, Sku, PriceText, Barcode, IsActive, Specs.Select(s => s.ToDraft()).ToList());

    public void ClearErrors()
    {
        NameError = null;
        SkuError = null;
        PriceError = null;
        BarcodeError = null;
        SpecsError = null;
        foreach (var s in Specs)
        {
            s.Error = null;
        }
    }
}
