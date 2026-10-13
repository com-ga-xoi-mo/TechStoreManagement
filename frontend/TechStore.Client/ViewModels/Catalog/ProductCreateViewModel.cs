using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductCreateViewModel : ObservableObject
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string brand = string.Empty;
    [ObservableProperty] private string categoryId = string.Empty;
    [ObservableProperty] private string productSku = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private bool isSerialTracked = true;
    [ObservableProperty] private string basePriceText = string.Empty;
    [ObservableProperty] private string costPriceText = string.Empty;
    [ObservableProperty] private string imageUrl = string.Empty;
    [ObservableProperty] private string imageUrlError = string.Empty;
    [ObservableProperty] private bool isLoadingCategories;
    [ObservableProperty] private bool isCategoriesError;
    [ObservableProperty] private string categoriesErrorMessage = string.Empty;
    [ObservableProperty] private bool isSubmitting;
    [ObservableProperty] private bool isSubmissionError;
    [ObservableProperty] private string submissionMessage = string.Empty;
    [ObservableProperty] private bool isCreated;
    [ObservableProperty] private ProductSummary? createdProduct;
    [ObservableProperty] private string firstInvalidFieldName = string.Empty;
    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private string brandError = string.Empty;
    [ObservableProperty] private string categoryError = string.Empty;
    [ObservableProperty] private string productSkuError = string.Empty;
    [ObservableProperty] private string barcodeError = string.Empty;
    [ObservableProperty] private string basePriceError = string.Empty;
    [ObservableProperty] private string costPriceError = string.Empty;
    [ObservableProperty] private string specificationsError = string.Empty;
    [ObservableProperty] private string variantsError = string.Empty;

    public ObservableCollection<CategoryOption> Categories { get; } = new();
    public ObservableCollection<ProductSpecificationEntryViewModel> Specifications { get; } = new();
    public ObservableCollection<ProductVariantDraftViewModel> Variants { get; } = new();
    public bool IsPreviewData => true;
    public string InventoryBoundaryMessage => "Tồn kho được nhập riêng sau khi tạo sản phẩm. Hàng quản lý Serial/IMEI cần nhập từng mã máy; luồng nhập kho chưa được triển khai.";

    public ProductCreateViewModel(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        Variants.Add(new ProductVariantDraftViewModel(RemoveVariant) { CanRemove = false });
    }

    [RelayCommand]
    public async Task LoadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        IsLoadingCategories = true;
        IsCategoriesError = false;
        CategoriesErrorMessage = string.Empty;
        try
        {
            var categories = await _categoryRepository.GetCategoriesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            if (Categories.All(category => category.Id != CategoryId))
            {
                CategoryId = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            IsCategoriesError = true;
            CategoriesErrorMessage = "Không thể tải danh mục preview. Hãy thử lại.";
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    [RelayCommand]
    public void AddSpecification() => Specifications.Add(new ProductSpecificationEntryViewModel(RemoveSpecification));

    [RelayCommand]
    public void RemoveSpecification(ProductSpecificationEntryViewModel? item)
    {
        if (item is not null)
        {
            Specifications.Remove(item);
        }
    }

    [RelayCommand]
    public void AddVariant()
    {
        Variants.Add(new ProductVariantDraftViewModel(RemoveVariant) { CanRemove = true });
        RefreshVariantRemoveState();
    }

    [RelayCommand]
    public void RemoveVariant(ProductVariantDraftViewModel? item)
    {
        if (item is not null && Variants.Count > 1)
        {
            Variants.Remove(item);
            RefreshVariantRemoveState();
        }
    }

    private void RefreshVariantRemoveState()
    {
        var canRemove = Variants.Count > 1;
        foreach (var variant in Variants)
        {
            variant.CanRemove = canRemove;
        }
    }

    [RelayCommand]
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (IsSubmitting || IsCreated)
        {
            return;
        }

        IsSubmissionError = false;
        SubmissionMessage = string.Empty;
        ClearValidation();
        if (!TryBuildDraft(out var draft))
        {
            return;
        }

        IsSubmitting = true;
        IsSubmissionError = false;
        SubmissionMessage = string.Empty;
        try
        {
            var result = await _productRepository.CreateProductAsync(draft!, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsSuccess)
            {
                IsCreated = true;
                CreatedProduct = result.Product;
                SubmissionMessage = "Đã tạo sản phẩm preview. Chưa ghi dữ liệu lên server; tồn kho cần nhập qua luồng riêng.";
                return;
            }

            IsSubmissionError = true;
            SubmissionMessage = result.Message ?? "Không thể tạo sản phẩm preview. Dữ liệu vẫn được giữ lại.";
            ApplyRepositoryFieldError(result.FieldName, SubmissionMessage);
        }
        catch (OperationCanceledException)
        {
            // A cancelled preview request leaves form values intact and can be retried.
        }
        catch (Exception)
        {
            IsSubmissionError = true;
            SubmissionMessage = "Không thể tạo sản phẩm preview. Dữ liệu vẫn được giữ lại.";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private bool TryBuildDraft(out ProductCreateDraft? draft)
    {
        draft = null;
        SetFirstError(nameof(Name), string.IsNullOrWhiteSpace(Name), "Vui lòng nhập tên sản phẩm.", value => NameError = value);
        SetFirstError(nameof(Brand), string.IsNullOrWhiteSpace(Brand), "Vui lòng nhập hãng sản xuất.", value => BrandError = value);
        SetFirstError(nameof(CategoryId), string.IsNullOrWhiteSpace(CategoryId) || Categories.All(category => category.Id != CategoryId), "Vui lòng chọn danh mục.", value => CategoryError = value);
        SetFirstError(nameof(ProductSku), string.IsNullOrWhiteSpace(ProductSku), "Vui lòng nhập SKU sản phẩm.", value => ProductSkuError = value);

        var hasBasePrice = TryParseMoney(BasePriceText, out var basePrice);
        SetFirstError(nameof(BasePriceText), !hasBasePrice || basePrice <= 0, "Giá bán phải lớn hơn 0.", value => BasePriceError = value);
        var hasCostPrice = TryParseMoney(CostPriceText, out var costPrice);
        SetFirstError(nameof(CostPriceText), !hasCostPrice || costPrice < 0, "Giá vốn phải là số không âm.", value => CostPriceError = value);

        for (var index = 0; index < Specifications.Count; index++)
        {
            var item = Specifications[index];
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
            {
                item.Error = "Nhập cả tên và giá trị thông số, hoặc xóa dòng này.";
                SetFirstInvalidField($"Specifications[{index}].Key");
                continue;
            }

            if (Specifications.Take(index).Any(previous => string.Equals(previous.Key.Trim(), item.Key.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                item.Error = "Tên thông số không được trùng.";
                SetFirstInvalidField($"Specifications[{index}].Key");
            }
        }

        if (Specifications.Any(item => !string.IsNullOrEmpty(item.Error)))
        {
            SpecificationsError = "Kiểm tra các thông số phần cứng.";
        }

        var normalizedImageUrl = NormalizeImageUrl(ImageUrl);
        if (!string.IsNullOrWhiteSpace(ImageUrl) && normalizedImageUrl is null)
        {
            ImageUrlError = "Ảnh preview cần là URL HTTP hoặc HTTPS hợp lệ.";
            SetFirstInvalidField(nameof(ImageUrl));
        }

        if (Variants.Count == 0)
        {
            SetFirstError("Variants", true, "Thêm ít nhất một biến thể.", value => VariantsError = value);
        }

        var variantDrafts = new ProductVariantDraft[Variants.Count];
        for (var index = 0; index < Variants.Count; index++)
        {
            var variant = Variants[index];
            variant.ClearErrors();
            if (string.IsNullOrWhiteSpace(variant.Name))
            {
                variant.NameError = "Vui lòng nhập tên biến thể.";
                SetFirstInvalidField($"Variants[{index}].Name");
            }

            if (string.IsNullOrWhiteSpace(variant.Sku))
            {
                variant.SkuError = "Vui lòng nhập SKU biến thể.";
                SetFirstInvalidField($"Variants[{index}].Sku");
            }

            var hasVariantPrice = TryParseMoney(variant.PriceText, out var variantPrice);
            if (!hasVariantPrice || variantPrice <= 0)
            {
                variant.PriceError = "Giá biến thể phải lớn hơn 0.";
                SetFirstInvalidField($"Variants[{index}].PriceText");
            }

            for (var specificationIndex = 0; specificationIndex < variant.Specifications.Count; specificationIndex++)
            {
                var specification = variant.Specifications[specificationIndex];
                if (string.IsNullOrWhiteSpace(specification.Key) || string.IsNullOrWhiteSpace(specification.Value))
                {
                    specification.Error = "Nhập cả tên và giá trị thông số, hoặc xóa dòng này.";
                    SetFirstInvalidField($"Variants[{index}].Specifications[{specificationIndex}].Key");
                }
            }

            if (variant.HasErrors)
            {
                continue;
            }

            variantDrafts[index] = new ProductVariantDraft(
                variant.Name.Trim(),
                variant.Sku.Trim(),
                NullIfBlank(variant.Barcode),
                variantPrice,
                variant.Specifications.Select(item => new ProductSpecificationEntry(item.Key.Trim(), item.Value.Trim())).ToArray());
        }

        var duplicateVariantSku = Variants
            .Select((item, index) => (item, index))
            .GroupBy(pair => pair.item.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Key.Length > 0 && group.Count() > 1);
        if (duplicateVariantSku is not null)
        {
            var duplicate = duplicateVariantSku.Last();
            duplicate.item.SkuError = "SKU biến thể phải khác nhau trong biểu mẫu.";
            SetFirstInvalidField($"Variants[{duplicate.index}].Sku");
        }

        var draftHasErrors = !string.IsNullOrEmpty(NameError) || !string.IsNullOrEmpty(BrandError) ||
            !string.IsNullOrEmpty(CategoryError) || !string.IsNullOrEmpty(ProductSkuError) ||
            !string.IsNullOrEmpty(BarcodeError) || !string.IsNullOrEmpty(ImageUrlError) || !string.IsNullOrEmpty(BasePriceError) ||
            !string.IsNullOrEmpty(CostPriceError) || !string.IsNullOrEmpty(SpecificationsError) ||
            Variants.Any(variant => variant.HasErrors);
        if (draftHasErrors)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Barcode) && Barcode.Any(character => !char.IsDigit(character)))
        {
            BarcodeError = "Barcode chỉ được chứa chữ số.";
            SetFirstInvalidField(nameof(Barcode));
            return false;
        }

        if (Variants.Any(variant => !string.IsNullOrWhiteSpace(variant.Barcode) && variant.Barcode.Any(character => !char.IsDigit(character))))
        {
            var invalidIndex = Variants.Select((item, index) => (item, index))
                .First(pair => !string.IsNullOrWhiteSpace(pair.item.Barcode) && pair.item.Barcode.Any(character => !char.IsDigit(character))).index;
            Variants[invalidIndex].BarcodeError = "Barcode chỉ được chứa chữ số.";
            SetFirstInvalidField($"Variants[{invalidIndex}].Barcode");
            return false;
        }

        draft = new ProductCreateDraft(
            Name.Trim(),
            Brand.Trim(),
            CategoryId,
            ProductSku.Trim(),
            NullIfBlank(Barcode),
            IsSerialTracked,
            basePrice,
            costPrice,
            normalizedImageUrl,
            Specifications.Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => new ProductSpecificationEntry(item.Key.Trim(), item.Value.Trim())).ToArray(),
            variantDrafts);
        return true;
    }

    private void ApplyRepositoryFieldError(string? fieldName, string message)
    {
        if (TryParseVariantField(fieldName, out var variantIndex, out var variantField) &&
            variantIndex >= 0 && variantIndex < Variants.Count)
        {
            var variant = Variants[variantIndex];
            if (variantField == "Sku")
            {
                variant.SkuError = message;
            }
            else if (variantField == "Barcode")
            {
                variant.BarcodeError = message;
            }

            SetFirstInvalidField(fieldName!);
            return;
        }

        switch (fieldName)
        {
            case nameof(ProductCreateDraft.ProductSku):
                ProductSkuError = message;
                SetFirstInvalidField(nameof(ProductSku));
                break;
            case nameof(ProductCreateDraft.Barcode):
                BarcodeError = message;
                SetFirstInvalidField(nameof(Barcode));
                break;
            default:
                IsSubmissionError = true;
                break;
        }
    }

    private static bool TryParseVariantField(string? fieldName, out int index, out string field)
    {
        index = -1;
        field = string.Empty;
        if (string.IsNullOrWhiteSpace(fieldName) || !fieldName.StartsWith("Variants[", StringComparison.Ordinal))
        {
            return false;
        }

        var closingBracket = fieldName.IndexOf(']');
        if (closingBracket < 9 || !int.TryParse(fieldName.AsSpan(9, closingBracket - 9), out index) ||
            closingBracket + 2 >= fieldName.Length || fieldName[closingBracket + 1] != '.')
        {
            return false;
        }

        field = fieldName[(closingBracket + 2)..];
        return true;
    }

    private void ClearValidation()
    {
        FirstInvalidFieldName = string.Empty;
        NameError = BrandError = CategoryError = ProductSkuError = BarcodeError = string.Empty;
        BasePriceError = CostPriceError = SpecificationsError = VariantsError = ImageUrlError = string.Empty;
        foreach (var item in Specifications)
        {
            item.Error = string.Empty;
        }

        foreach (var variant in Variants)
        {
            variant.ClearErrors();
            foreach (var item in variant.Specifications)
            {
                item.Error = string.Empty;
            }
        }
    }

    private void SetFirstError(string fieldName, bool isInvalid, string message, Action<string> setError)
    {
        if (!isInvalid)
        {
            return;
        }

        setError(message);
        SetFirstInvalidField(fieldName);
    }

    private void SetFirstInvalidField(string fieldName)
    {
        if (string.IsNullOrEmpty(FirstInvalidFieldName))
        {
            FirstInvalidFieldName = fieldName;
        }
    }

    private static bool TryParseMoney(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out result);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeImageUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.ToString()
            : null;
}

public sealed partial class ProductSpecificationEntryViewModel : ObservableObject
{
    private readonly Action<ProductSpecificationEntryViewModel>? _remove;

    [ObservableProperty] private string key = string.Empty;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private string error = string.Empty;

    public ProductSpecificationEntryViewModel(Action<ProductSpecificationEntryViewModel>? remove = null) => _remove = remove;

    [RelayCommand]
    public void Remove() => _remove?.Invoke(this);
}

public sealed partial class ProductVariantDraftViewModel : ObservableObject
{
    private readonly Action<ProductVariantDraftViewModel>? _remove;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private string priceText = string.Empty;
    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private string skuError = string.Empty;
    [ObservableProperty] private string barcodeError = string.Empty;
    [ObservableProperty] private string priceError = string.Empty;
    [ObservableProperty] private bool canRemove;

    public ObservableCollection<ProductSpecificationEntryViewModel> Specifications { get; } = new();
    public bool HasErrors => !string.IsNullOrEmpty(NameError) || !string.IsNullOrEmpty(SkuError) ||
        !string.IsNullOrEmpty(BarcodeError) || !string.IsNullOrEmpty(PriceError) || Specifications.Any(item => !string.IsNullOrEmpty(item.Error));

    public ProductVariantDraftViewModel(Action<ProductVariantDraftViewModel>? remove = null) => _remove = remove;

    [RelayCommand]
    public void AddSpecification() => Specifications.Add(new ProductSpecificationEntryViewModel(RemoveSpecification));

    [RelayCommand]
    public void RemoveSpecification(ProductSpecificationEntryViewModel? item)
    {
        if (item is not null)
        {
            Specifications.Remove(item);
        }
    }

    [RelayCommand]
    public void Remove() => _remove?.Invoke(this);

    public void ClearErrors()
    {
        NameError = SkuError = BarcodeError = PriceError = string.Empty;
    }
}
