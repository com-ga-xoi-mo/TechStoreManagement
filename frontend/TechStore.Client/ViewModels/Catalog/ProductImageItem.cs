using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Files;

namespace TechStore.Client.ViewModels.Catalog;

public sealed record ImagePreview(string? Url, byte[]? Bytes);

public sealed partial class ProductImageItem : ObservableObject
{
    public Guid? Id { get; private set; }
    public string? Url { get; private set; }
    public byte[]? PendingContent { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    [ObservableProperty] private VariantRowItem? variant;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isCover;
    public bool IsNew => Id is null;
    public string VariantLabel => Variant?.VariantName ?? string.Empty;
    public bool HasVariantLabel => VariantLabel.Length > 0;
    public ImagePreview Preview => new(Url, PendingContent);

    partial void OnVariantChanging(VariantRowItem? value)
    {
        if (Variant != null) Variant.PropertyChanged -= RowChanged;
    }
    partial void OnVariantChanged(VariantRowItem? value)
    {
        if (value != null) value.PropertyChanged += RowChanged;
        RaiseLabel();
    }
    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VariantRowItem.VariantName)) RaiseLabel();
    }
    private void RaiseLabel()
    {
        OnPropertyChanged(nameof(VariantLabel)); OnPropertyChanged(nameof(HasVariantLabel));
    }
    public void MarkSaved(Guid id, string url)
    {
        Id = id; Url = url; PendingContent = null;
        OnPropertyChanged(nameof(Id)); OnPropertyChanged(nameof(Url)); OnPropertyChanged(nameof(PendingContent));
        OnPropertyChanged(nameof(IsNew)); OnPropertyChanged(nameof(Preview));
    }
    public static ProductImageItem FromSaved(ProductImage image, VariantRowItem? row) => new()
    {
        Id = image.Id, Url = image.Url, ContentType = image.ContentType, Variant = row
    };
    public static ProductImageItem FromPicked(PickedImageFile file) => new()
    {
        FileName = file.FileName, ContentType = ProductImageRules.ContentTypeFor(file.FileName), PendingContent = file.Content
    };
}
