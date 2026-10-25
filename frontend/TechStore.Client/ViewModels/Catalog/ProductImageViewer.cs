using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public sealed record ImageFilterChoice(string Label, Guid? VariantId, bool IsAll);

public sealed partial class ProductImageViewer : ObservableObject
{
    private ProductDetail? _product;
    [ObservableProperty] private IReadOnlyList<ImageFilterChoice> filters = [];
    [ObservableProperty] private ImageFilterChoice? selectedFilter;
    [ObservableProperty] private ProductImageItem? selectedImage;
    public ObservableCollection<ProductImageItem> VisibleImages { get; } = [];
    public string? SelectedUrl => SelectedImage?.Url;
    public bool HasThumbnails => VisibleImages.Count >= 2;
    public bool HasFilter => Filters.Count > 1;

    public void Load(ProductDetail? product)
    {
        _product = product;
        List<ImageFilterChoice> choices = [new("Tất cả", null, true)];
        if (product != null)
            choices.AddRange(product.Variants.Where(v => product.Images.Any(i => i.VariantId == v.Id)).Select(v => new ImageFilterChoice(v.VariantName, v.Id, false)));
        Filters = choices; SelectedFilter = choices[0]; Rebuild(); OnPropertyChanged(nameof(HasFilter));
    }
    partial void OnSelectedFilterChanged(ImageFilterChoice? value) => Rebuild();
    partial void OnSelectedImageChanged(ProductImageItem? value)
    {
        foreach (var image in VisibleImages) image.IsSelected = ReferenceEquals(image, value);
        OnPropertyChanged(nameof(SelectedUrl));
    }
    private void Rebuild()
    {
        VisibleImages.Clear();
        var visible = (_product?.Images ?? []).Where(i => SelectedFilter?.IsAll != false || i.VariantId == null || i.VariantId == SelectedFilter.VariantId).OrderBy(i => i.SortOrder).ToList();
        foreach (var image in visible)
        {
            var variant = _product?.Variants.FirstOrDefault(v => v.Id == image.VariantId);
            var row = variant == null ? null : new VariantRowItem(variant.Id) { VariantName = variant.VariantName };
            VisibleImages.Add(ProductImageItem.FromSaved(image, row));
        }
        var preferred = SelectedFilter?.IsAll == false ? visible.FirstOrDefault(i => i.VariantId == SelectedFilter.VariantId)?.Id : null;
        SelectedImage = VisibleImages.FirstOrDefault(i => i.Id == preferred) ?? VisibleImages.FirstOrDefault();
        OnPropertyChanged(nameof(HasThumbnails));
    }
    [RelayCommand]
    private void SelectImage(object? parameter)
    {
        if (parameter is ProductImageItem image && VisibleImages.Contains(image)) SelectedImage = image;
    }
}
