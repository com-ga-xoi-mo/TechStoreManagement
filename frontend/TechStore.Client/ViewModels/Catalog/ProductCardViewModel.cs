using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductCardViewModel : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string Brand { get; }
    public string? ImageUrl { get; }
    public ICommand? OpenDetailCommand { get; }
    public ICommand? OpenEditCommand { get; }
    [ObservableProperty]
    private bool isEditActionVisible;
    
    // Original entity reference
    private readonly ProductSummary _product;

    public ObservableCollection<ProductVariantSummary> Variants { get; } = new();

    [ObservableProperty]
    private ProductVariantSummary? selectedVariant;

    [ObservableProperty]
    private decimal currentPrice;

    [ObservableProperty]
    private string currentSku = string.Empty;

    [ObservableProperty]
    private int currentAvailableQuantity;

    [ObservableProperty]
    private string? currentSwatchLabel;

    [ObservableProperty]
    private string? currentSwatchColor;

    [ObservableProperty]
    private ProductStatus currentStatus;

    public ProductCardViewModel(
        ProductSummary product,
        ICommand? openDetailCommand = null,
        ICommand? openEditCommand = null,
        bool canEdit = false)
    {
        _product = product;
        OpenDetailCommand = openDetailCommand;
        OpenEditCommand = openEditCommand;
        IsEditActionVisible = canEdit;
        Id = product.Id;
        Name = product.Name;
        Brand = product.Brand;
        ImageUrl = product.ImageUrl;
        CurrentStatus = product.Status;

        foreach (var variant in product.Variants)
        {
            Variants.Add(variant);
        }

        SelectedVariant = Variants.FirstOrDefault();
    }

    partial void OnSelectedVariantChanged(ProductVariantSummary? value)
    {
        if (value != null)
        {
            CurrentPrice = value.SellingPrice;
            CurrentSku = value.Sku;
            CurrentAvailableQuantity = value.AvailableQuantity;
            CurrentSwatchLabel = value.SwatchLabel;
            CurrentSwatchColor = value.SwatchColor;

            if (_product.Status == ProductStatus.Discontinued)
            {
                CurrentStatus = ProductStatus.Discontinued;
            }
            else if (value.AvailableQuantity <= 0)
            {
                CurrentStatus = ProductStatus.OutOfStock;
            }
            else if (_product.Status == ProductStatus.LowStock)
            {
                CurrentStatus = ProductStatus.LowStock;
            }
            else
            {
                CurrentStatus = _product.Status;
            }
        }
    }
}
