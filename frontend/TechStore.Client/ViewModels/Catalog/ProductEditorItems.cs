using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class SpecRowItem : ObservableObject
{
    [ObservableProperty] private string key = string.Empty;
    [ObservableProperty] private int kindIndex;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private string? error;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public SpecDraft ToDraft()
    {
        var kind = KindIndex switch
        {
            1 => SpecValueKind.Number,
            2 => SpecValueKind.Boolean,
            _ => SpecValueKind.Text
        };
        return new SpecDraft(Key, kind, Value);
    }

    public static SpecRowItem From(SpecDraft d)
    {
        var kindIndex = d.Kind switch
        {
            SpecValueKind.Number => 1,
            SpecValueKind.Boolean => 2,
            _ => 0
        };
        return new SpecRowItem
        {
            Key = d.Key,
            KindIndex = kindIndex,
            Value = d.Value
        };
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
