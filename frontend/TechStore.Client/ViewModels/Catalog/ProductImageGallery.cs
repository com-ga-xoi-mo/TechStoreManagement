using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Files;

namespace TechStore.Client.ViewModels.Catalog;

public sealed record ImageVariantChoice(string Label, VariantRowItem? Row);

public sealed partial class ProductImageGallery : ObservableObject
{
    private readonly IImageFilePicker _picker;
    private readonly ObservableCollection<VariantRowItem> _variants;
    private readonly Func<CancellationToken> _token;
    private readonly HashSet<VariantRowItem> _trackedRows = [];
    private readonly HashSet<ProductImageItem> _trackedImages = [];
    private bool _rebuildingChoices;
    private bool _refreshing;
    [ObservableProperty] private ProductImageItem? selectedImage;
    [ObservableProperty] private ImageVariantChoice? selectedVariantChoice;
    [ObservableProperty] private string pickMessage = string.Empty;
    public ObservableCollection<ProductImageItem> Images { get; } = [];
    public ObservableCollection<ImageVariantChoice> VariantChoices { get; } = [];
    public string CountText => $"{Images.Count}/20";
    public bool CanAddMore => Images.Count < ProductImageRules.MaxImages;
    public bool HasPickMessage => PickMessage.Length > 0;
    public bool HasSelection => SelectedImage != null;
    public ImagePreview? LargePreview => SelectedImage?.Preview;
    public bool ShowMakeCover => SelectedImage is { Variant: null, IsCover: false };
    public bool CanMoveLeft => SelectedImage != null && Images.IndexOf(SelectedImage) > 0;
    public bool CanMoveRight => SelectedImage != null && Images.IndexOf(SelectedImage) < Images.Count - 1;

    public ProductImageGallery(IImageFilePicker picker, ObservableCollection<VariantRowItem> variants, Func<CancellationToken> token)
    {
        _picker = picker; _variants = variants; _token = token;
        Images.CollectionChanged += ImagesChanged;
        variants.CollectionChanged += VariantsChanged;
        TrackRows(); RebuildChoices();
    }

    partial void OnPickMessageChanged(string value) => OnPropertyChanged(nameof(HasPickMessage));
    partial void OnSelectedImageChanged(ProductImageItem? value)
    {
        foreach (var image in Images) image.IsSelected = ReferenceEquals(image, value);
        RestoreChoice(); Refresh();
    }
    partial void OnSelectedVariantChoiceChanged(ImageVariantChoice? value)
    {
        if (_rebuildingChoices) return;
        if (value == null) { RestoreChoice(); return; }
        if (SelectedImage != null) SelectedImage.Variant = value.Row;
    }
    private void ImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var image in _trackedImages.Where(i => !Images.Contains(i)).ToArray())
        {
            image.PropertyChanged -= ImageChanged; _trackedImages.Remove(image);
        }
        foreach (var image in Images.Where(i => !_trackedImages.Contains(i)))
        {
            image.PropertyChanged += ImageChanged; _trackedImages.Add(image);
        }
        Refresh();
    }
    private void ImageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProductImageItem.Variant) or nameof(ProductImageItem.Preview) or nameof(ProductImageItem.IsNew))
        {
            RestoreChoice(); Refresh();
        }
    }
    private void VariantsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var image in Images.Where(i => i.Variant != null && !_variants.Contains(i.Variant))) image.Variant = null;
        TrackRows(); RebuildChoices(); Refresh();
    }
    private void TrackRows()
    {
        foreach (var row in _trackedRows.Where(r => !_variants.Contains(r)).ToArray())
        {
            row.PropertyChanged -= RowChanged; row.ImageCount = 0; _trackedRows.Remove(row);
        }
        foreach (var row in _variants.Where(r => !_trackedRows.Contains(r)))
        {
            row.PropertyChanged += RowChanged; _trackedRows.Add(row);
        }
    }
    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VariantRowItem.VariantName)) RebuildChoices();
    }
    private void RebuildChoices()
    {
        _rebuildingChoices = true;
        try
        {
            VariantChoices.Clear(); VariantChoices.Add(new("Chung cho mọi biến thể", null));
            foreach (var row in _variants) VariantChoices.Add(new(row.VariantName, row));
            SelectedVariantChoice = VariantChoices.First(c => ReferenceEquals(c.Row, SelectedImage?.Variant));
        }
        finally { _rebuildingChoices = false; }
    }
    private void RestoreChoice()
    {
        _rebuildingChoices = true;
        try { SelectedVariantChoice = VariantChoices.FirstOrDefault(c => ReferenceEquals(c.Row, SelectedImage?.Variant)); }
        finally { _rebuildingChoices = false; }
    }
    private void Refresh()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var cover = ProductImageRules.CoverIndex(Images, i => i.Variant == null);
            for (var i = 0; i < Images.Count; i++) Images[i].IsCover = i == cover;
            foreach (var row in _variants) row.ImageCount = Images.Count(i => ReferenceEquals(i.Variant, row));
            foreach (var name in new[] { nameof(CountText), nameof(CanAddMore), nameof(HasSelection), nameof(LargePreview), nameof(ShowMakeCover), nameof(CanMoveLeft), nameof(CanMoveRight) }) OnPropertyChanged(name);
            AddImagesCommand.NotifyCanExecuteChanged(); MakeCoverCommand.NotifyCanExecuteChanged();
            MoveLeftCommand.NotifyCanExecuteChanged(); MoveRightCommand.NotifyCanExecuteChanged(); RemoveSelectedCommand.NotifyCanExecuteChanged();
        }
        finally { _refreshing = false; }
    }
    public List<Guid> RemovedImageIds { get; } = [];

    [RelayCommand(CanExecute = nameof(CanAddMore))]
    private async Task AddImagesAsync()
    {
        try
        {
            var picked = await _picker.PickImagesAsync(ProductImageRules.MaxBytes, _token());
            if (picked.Count == 0) return;
            var filtered = ProductImageRules.FilterPicked(picked, Images.Count);
            ProductImageItem? first = null;
            foreach (var file in filtered.Accepted)
            {
                var item = ProductImageItem.FromPicked(file); Images.Add(item); first ??= item;
            }
            if (first != null) SelectedImage = first;
            PickMessage = string.Join(Environment.NewLine, filtered.Messages);
        }
        catch (InvalidOperationException ex) { PickMessage = ex.Message; }
    }
    [RelayCommand]
    private void SelectImage(object? parameter)
    {
        if (parameter is ProductImageItem item && Images.Contains(item)) SelectedImage = item;
    }
    [RelayCommand(CanExecute = nameof(ShowMakeCover))]
    private void MakeCover()
    {
        if (ShowMakeCover) Images.Move(Images.IndexOf(SelectedImage!), 0);
    }
    [RelayCommand(CanExecute = nameof(CanMoveLeft))]
    private void MoveLeft()
    {
        if (CanMoveLeft) { var i = Images.IndexOf(SelectedImage!); Images.Move(i, i - 1); }
    }
    [RelayCommand(CanExecute = nameof(CanMoveRight))]
    private void MoveRight()
    {
        if (CanMoveRight) { var i = Images.IndexOf(SelectedImage!); Images.Move(i, i + 1); }
    }
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveSelected()
    {
        if (SelectedImage == null) return;
        var index = Images.IndexOf(SelectedImage);
        if (index < 0) return;
        if (SelectedImage.Id is { } id) RemovedImageIds.Add(id);
        var removed = SelectedImage;
        Images.RemoveAt(index); removed.Variant = null;
        SelectedImage = Images.Count == 0 ? null : Images[Math.Min(index, Images.Count - 1)];
    }

    private IReadOnlyList<(Guid? Id, object? Variant)> _baseline = [];
    private IReadOnlyList<(Guid? Id, object? Variant)> Snapshot() =>
        Images.Select(i => (i.Id, i.Variant?.Id is { } id ? (object)id : i.Variant)).ToArray();
    public bool HasChanges => RemovedImageIds.Count > 0 || Images.Any(i => i.IsNew) || !Snapshot().SequenceEqual(_baseline);
    public void Load(IReadOnlyList<ProductImage> saved, IReadOnlyList<VariantRowItem> rows)
    {
        Reset();
        foreach (var image in saved.OrderBy(i => i.SortOrder))
            Images.Add(ProductImageItem.FromSaved(image, image.VariantId is { } id ? rows.FirstOrDefault(r => r.Id == id) : null));
        SelectedImage = Images.FirstOrDefault(); AcceptChanges();
    }
    public void Reset()
    {
        foreach (var image in Images) image.Variant = null;
        Images.Clear(); SelectedImage = null; RemovedImageIds.Clear(); PickMessage = string.Empty;
        _baseline = []; Refresh();
    }
    public void AcceptChanges() { _baseline = Snapshot(); RemovedImageIds.Clear(); }
    public void ForgetRemoved(Guid id) => RemovedImageIds.Remove(id);

}
