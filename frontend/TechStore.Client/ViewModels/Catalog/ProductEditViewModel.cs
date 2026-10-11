using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Catalog;

/// <summary>
/// Holds an isolated, unsaved product-edit draft. It has no dependency on XAML or HTTP.
/// </summary>
public sealed partial class ProductEditViewModel : ObservableObject, IDisposable
{
    public const string EditCapability = "catalog:product-edit";
    public const string CostWriteCapability = "catalog:cost-write";

    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICurrentSession _currentSession;
    private CancellationTokenSource? _loadCancellation;
    private long _loadGeneration;
    private ProductEditData? _loadedProduct;
    private bool _disposed;

    [ObservableProperty] private string productId = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string brand = string.Empty;
    [ObservableProperty] private string categoryId = string.Empty;
    [ObservableProperty] private string productSku = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private string modelId = string.Empty;
    [ObservableProperty] private string imageUrl = string.Empty;
    [ObservableProperty] private bool isImagePreviewError;
    [ObservableProperty] private string basePriceText = string.Empty;
    [ObservableProperty] private string costPriceText = string.Empty;
    [ObservableProperty] private bool isSerialTracked;
    [ObservableProperty] private int availableQuantity;
    [ObservableProperty] private bool isPreviewData;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isLoadError;
    [ObservableProperty] private bool isUnavailable;
    [ObservableProperty] private string loadMessage = string.Empty;
    [ObservableProperty] private bool isLoadingCategories;
    [ObservableProperty] private bool isCategoryError;
    [ObservableProperty] private string categoryMessage = string.Empty;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool isSaveError;
    [ObservableProperty] private string saveMessage = string.Empty;
    [ObservableProperty] private bool isSaved;
    [ObservableProperty] private ProductEditData? savedProduct;
    [ObservableProperty] private bool isCancelled;
    [ObservableProperty] private string firstInvalidFieldName = string.Empty;
    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private string brandError = string.Empty;
    [ObservableProperty] private string categoryError = string.Empty;
    [ObservableProperty] private string productSkuError = string.Empty;
    [ObservableProperty] private string barcodeError = string.Empty;
    [ObservableProperty] private string basePriceError = string.Empty;
    [ObservableProperty] private string costPriceError = string.Empty;
    [ObservableProperty] private string specificationsError = string.Empty;
    [ObservableProperty] private string repositoryFieldName = string.Empty;
    [ObservableProperty] private bool hasEditCapability;
    [ObservableProperty] private bool hasCostWriteCapability;
    [ObservableProperty] private ProductEditVariantViewModel? selectedVariant;

    public ObservableCollection<CategoryOption> Categories { get; } = new();
    public ObservableCollection<ProductEditSpecificationViewModel> Specifications { get; } = new();
    public ObservableCollection<ProductEditVariantViewModel> Variants { get; } = new();
    public bool IsPopulated => _loadedProduct is not null && !IsLoading && !IsLoadError && !IsUnavailable;
    public bool CanEdit => HasEditCapability && IsPopulated && !IsSaving && !IsSaved;
    public bool IsSerialTrackingReadOnly => true;
    public bool IsCostFieldsVisible => HasCostWriteCapability && HasEditCapability;
    public bool HasSelectedVariant => SelectedVariant is not null;
    public string ProvenanceMessage => IsPreviewData ? "Dữ liệu mẫu trong chế độ UI preview; chưa lưu lên server." : string.Empty;
    public string SkuSuggestionMessage => "Gợi ý SKU chỉ để tham khảo; hệ thống chưa kiểm tra tính duy nhất.";
    public string ImagePreviewMessage => IsImagePreviewError
        ? "Không tải được ảnh từ URL này. Hãy kiểm tra link; upload ảnh chưa triển khai."
        : "Ảnh chỉ được xem trước từ URL; upload/CDN chưa triển khai.";

    public ProductEditViewModel(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        ICurrentSession currentSession)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        RefreshCapabilities();
        _currentSession.SessionChanged += OnSessionChanged;
    }

    public async Task LoadAsync(string productId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        CancelLoad();
        var generation = Interlocked.Increment(ref _loadGeneration);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;
        ProductId = productId;
        ClearLoadedProduct();
        IsLoading = true;
        IsLoadError = false;
        IsUnavailable = false;
        LoadMessage = string.Empty;
        IsSaved = false;
        IsCancelled = false;

        try
        {
            if (!HasEditCapability)
            {
                IsUnavailable = true;
                LoadMessage = "Phiên preview hiện tại không có quyền mở chức năng sửa sản phẩm.";
                return;
            }

            var categoryTask = LoadCategoriesAsync(cancellation.Token);
            var productTask = _productRepository.GetProductForEditAsync(
                new ProductEditQuery(productId, IncludeCostFields: HasCostWriteCapability), cancellation.Token);
            await Task.WhenAll(categoryTask, productTask);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _loadGeneration))
            {
                return;
            }

            var product = await productTask;
            if (product is null)
            {
                IsUnavailable = true;
                LoadMessage = "Không thể tìm thấy sản phẩm trong dữ liệu preview.";
                return;
            }

            _loadedProduct = product;
            CopyToDraft(product);
        }
        catch (OperationCanceledException)
        {
            // Closing the dialog or replacing the product abandons this isolated load.
        }
        catch (Exception)
        {
            if (generation == Volatile.Read(ref _loadGeneration))
            {
                IsLoadError = true;
                LoadMessage = "Không thể tải dữ liệu sửa sản phẩm. Vui lòng thử lại.";
            }
        }
        finally
        {
            if (generation == Volatile.Read(ref _loadGeneration))
            {
                IsLoading = false;
            }

            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Dispose();
            NotifyDerivedState();
        }
    }

    [RelayCommand]
    public void AddSpecification() => Specifications.Add(new ProductEditSpecificationViewModel(remove: RemoveSpecification));

    [RelayCommand]
    public void RemoveSpecification(ProductEditSpecificationViewModel? item)
    {
        if (item is not null)
        {
            Specifications.Remove(item);
        }
    }

    [RelayCommand]
    public void SuggestSku(ProductEditVariantViewModel? variant)
    {
        variant ??= SelectedVariant;
        if (variant is null)
        {
            return;
        }

        var parts = new[] { ModelId, variant.StorageLabel, variant.ColorLabel }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeSkuPart)
            .Where(value => value.Length > 0);
        var suggestion = string.Join('-', parts);
        if (suggestion.Length == 0)
        {
            return;
        }

        variant.SetSuggestedSku(suggestion);
    }

    [RelayCommand]
    public void AddVariant()
    {
        var variant = new ProductEditVariantViewModel(id: null, RemoveVariant, SuggestSku)
        {
            CanRemove = true,
            CanEditCost = HasCostWriteCapability
        };
        Variants.Add(variant);
        RefreshVariantRemoveState();
        SelectedVariant = variant;
    }

    [RelayCommand]
    public void RemoveVariant(ProductEditVariantViewModel? item)
    {
        if (item is null || Variants.Count <= 1)
        {
            return;
        }

        var index = Variants.IndexOf(item);
        Variants.Remove(item);
        RefreshVariantRemoveState();
        if (ReferenceEquals(SelectedVariant, item))
        {
            SelectedVariant = Variants[Math.Clamp(index, 0, Variants.Count - 1)];
        }
    }

    public bool SelectVariant(string? variantId)
    {
        if (string.IsNullOrWhiteSpace(variantId))
        {
            return false;
        }

        var variant = Variants.FirstOrDefault(item => item.Id == variantId);
        if (variant is null)
        {
            return false;
        }

        SelectedVariant = variant;
        OnPropertyChanged(nameof(HasSelectedVariant));
        return true;
    }

    [RelayCommand]
    public async Task RetryAsync()
    {
        if (!string.IsNullOrWhiteSpace(ProductId))
        {
            await LoadAsync(ProductId);
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        if (IsSaving || IsSaved)
        {
            return;
        }

        IsCancelled = true;
        CancelLoad();
    }

    [RelayCommand]
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (!CanEdit || !TryBuildDraft(out var draft))
        {
            return;
        }

        IsSaving = true;
        IsSaveError = false;
        SaveMessage = string.Empty;
        RepositoryFieldName = string.Empty;
        try
        {
            var result = await _productRepository.UpdateProductAsync(draft!, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsSuccess)
            {
                _loadedProduct = result.Product;
                SavedProduct = result.Product;
                IsSaved = true;
                SaveMessage = IsPreviewData
                    ? "Đã lưu thay đổi trong dữ liệu preview; chưa ghi lên server."
                    : "Đã lưu thay đổi sản phẩm.";
                return;
            }

            IsSaveError = true;
            SaveMessage = result.Message ?? "Không thể lưu thay đổi. Dữ liệu đang nhập vẫn được giữ lại.";
            RepositoryFieldName = result.FieldName ?? string.Empty;
            ApplyRepositoryFieldError(RepositoryFieldName, SaveMessage);
        }
        catch (OperationCanceledException)
        {
            // Keep the unsaved input available to retry.
        }
        catch (Exception)
        {
            IsSaveError = true;
            SaveMessage = "Không thể lưu thay đổi. Dữ liệu đang nhập vẫn được giữ lại.";
        }
        finally
        {
            IsSaving = false;
            NotifyDerivedState();
        }
    }

    public void CancelLoad()
    {
        Interlocked.Increment(ref _loadGeneration);
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    public void ReportImagePreviewFailure() => IsImagePreviewError = true;

    partial void OnImageUrlChanged(string value)
    {
        if (IsImagePreviewError)
        {
            IsImagePreviewError = false;
        }
    }

    partial void OnIsImagePreviewErrorChanged(bool value) => OnPropertyChanged(nameof(ImagePreviewMessage));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelLoad();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _currentSession.SessionChanged -= OnSessionChanged;
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        IsLoadingCategories = true;
        IsCategoryError = false;
        CategoryMessage = string.Empty;
        try
        {
            var categories = await _categoryRepository.GetCategoriesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            IsCategoryError = true;
            CategoryMessage = "Không thể tải danh mục preview.";
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    private bool TryBuildDraft(out ProductEditDraft? draft)
    {
        draft = null;
        FirstInvalidFieldName = string.Empty;
        NameError = BrandError = CategoryError = ProductSkuError = BarcodeError = string.Empty;
        BasePriceError = CostPriceError = SpecificationsError = string.Empty;
        SetError(nameof(Name), string.IsNullOrWhiteSpace(Name), "Vui lòng nhập tên sản phẩm.", value => NameError = value);
        SetError(nameof(Brand), string.IsNullOrWhiteSpace(Brand), "Vui lòng nhập thương hiệu.", value => BrandError = value);
        SetError(nameof(CategoryId), string.IsNullOrWhiteSpace(CategoryId), "Vui lòng chọn danh mục.", value => CategoryError = value);
        SetError(nameof(ProductSku), string.IsNullOrWhiteSpace(ProductSku), "Vui lòng nhập SKU sản phẩm.", value => ProductSkuError = value);

        var hasBasePrice = decimal.TryParse(BasePriceText, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var basePrice);
        SetError(nameof(BasePriceText), !hasBasePrice || basePrice < 0, "Giá bán phải là số không âm.", value => BasePriceError = value);
        decimal? costPrice = null;
        if (HasCostWriteCapability)
        {
            var hasCost = decimal.TryParse(CostPriceText, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var parsedCost);
            SetError(nameof(CostPriceText), !hasCost || parsedCost < 0, "Giá vốn phải là số không âm.", value => CostPriceError = value);
            if (hasCost)
            {
                costPrice = parsedCost;
            }
        }

        if (Variants.Count == 0)
        {
            SetFirstInvalidField("Variants");
            return false;
        }

        var variantDrafts = new ProductEditVariantDraft[Variants.Count];
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

            var hasPrice = decimal.TryParse(variant.SellingPriceText, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var price);
            if (!hasPrice || price < 0)
            {
                variant.PriceError = "Giá bán biến thể phải là số không âm.";
                SetFirstInvalidField($"Variants[{index}].SellingPriceText");
            }

            var hasVariantCost = decimal.TryParse(variant.CostPriceText, NumberStyles.Number,
                CultureInfo.GetCultureInfo("vi-VN"), out var parsedCostPrice);
            if (HasCostWriteCapability && !string.IsNullOrWhiteSpace(variant.CostPriceText) && !hasVariantCost)
            {
                variant.CostPriceError = "Giá vốn phải là số không âm.";
                SetFirstInvalidField($"Variants[{index}].CostPriceText");
            }
            else if (HasCostWriteCapability && hasVariantCost && parsedCostPrice < 0)
            {
                variant.CostPriceError = "Giá vốn phải là số không âm.";
                SetFirstInvalidField($"Variants[{index}].CostPriceText");
            }

            var rowSpecificationsError = string.Empty;
            ValidateSpecifications(variant.Specifications, $"Variants[{index}].Specifications", ref rowSpecificationsError);
            variant.SpecificationsError = rowSpecificationsError;

            if (variant.HasErrors)
            {
                continue;
            }

            variantDrafts[index] = new ProductEditVariantDraft(
                variant.Id,
                variant.Name.Trim(),
                variant.Sku.Trim(),
                NullIfBlank(variant.Barcode),
                NullIfBlank(variant.StorageLabel),
                NullIfBlank(variant.ColorLabel),
                NullIfBlank(variant.SwatchColor),
                price,
                HasCostWriteCapability ? ParseOptionalMoney(variant.CostPriceText) : null,
                variant.Specifications.Select(item => new ProductSpecificationEntry(item.Key.Trim(), item.Value.Trim())).ToArray());
        }

        var productSpecificationsError = string.Empty;
        ValidateSpecifications(Specifications, "Specifications", ref productSpecificationsError);
        SpecificationsError = productSpecificationsError;

        if (!string.IsNullOrEmpty(FirstInvalidFieldName) || Variants.Any(variant => variant.HasErrors) ||
            Specifications.Any(item => !string.IsNullOrEmpty(item.Error)))
        {
            return false;
        }

        draft = new ProductEditDraft(
            ProductId,
            Name.Trim(),
            Brand.Trim(),
            CategoryId,
            ProductSku.Trim(),
            NullIfBlank(Barcode),
            NullIfBlank(ModelId),
            NullIfBlank(ImageUrl),
            basePrice,
            costPrice,
            _loadedProduct?.IsSerialTracked ?? IsSerialTracked,
            Specifications.Select(item => new ProductSpecificationEntry(item.Key.Trim(), item.Value.Trim())).ToArray(),
            variantDrafts);
        return true;
    }

    private void ValidateSpecifications(
        ObservableCollection<ProductEditSpecificationViewModel> specifications,
        string fieldPrefix,
        ref string errorSummary)
    {
        for (var index = 0; index < specifications.Count; index++)
        {
            var specification = specifications[index];
            specification.Error = string.Empty;
            if (string.IsNullOrWhiteSpace(specification.Key) || string.IsNullOrWhiteSpace(specification.Value))
            {
                specification.Error = "Nhập cả tên và giá trị thông số, hoặc xóa dòng này.";
                errorSummary = "Kiểm tra các thông số kỹ thuật.";
                SetFirstInvalidField($"{fieldPrefix}[{index}].Key");
                continue;
            }

            if (specifications.Take(index).Any(previous =>
                string.Equals(previous.Key.Trim(), specification.Key.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                specification.Error = "Tên thông số không được trùng.";
                errorSummary = "Kiểm tra các thông số kỹ thuật.";
                SetFirstInvalidField($"{fieldPrefix}[{index}].Key");
            }
        }
    }

    private void ApplyRepositoryFieldError(string fieldName, string message)
    {
        if (fieldName.Contains("Variants[", StringComparison.Ordinal) &&
            int.TryParse(fieldName.AsSpan("Variants[".Length, fieldName.IndexOf(']') - "Variants[".Length), out var index) &&
            index >= 0 && index < Variants.Count)
        {
            var field = fieldName[(fieldName.IndexOf(']') + 2)..];
            if (field == "Sku") Variants[index].SkuError = message;
            if (field == "Barcode") Variants[index].BarcodeError = message;
        }
        else if (fieldName is nameof(ProductEditDraft.ProductSku))
        {
            ProductSkuError = message;
        }
        else if (fieldName is nameof(ProductEditDraft.Barcode))
        {
            BarcodeError = message;
        }

        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            SetFirstInvalidField(fieldName);
        }
    }

    private void CopyToDraft(ProductEditData product)
    {
        Name = product.Name;
        Brand = product.Brand;
        CategoryId = product.CategoryId;
        ProductSku = product.ProductSku;
        Barcode = product.Barcode ?? string.Empty;
        ModelId = product.ModelId ?? string.Empty;
        ImageUrl = product.ImageUrl ?? string.Empty;
        BasePriceText = FormatMoney(product.BasePrice);
        CostPriceText = HasCostWriteCapability ? FormatMoney(product.CostPrice) : string.Empty;
        IsSerialTracked = product.IsSerialTracked;
        AvailableQuantity = product.AvailableQuantity;
        IsPreviewData = product.IsPreviewData;
        Specifications.Clear();
        foreach (var specification in product.Specifications)
        {
            Specifications.Add(new ProductEditSpecificationViewModel(specification.Key, specification.Value, RemoveSpecification));
        }

        Variants.Clear();
        foreach (var variant in product.Variants)
        {
            var row = new ProductEditVariantViewModel(variant.Id, RemoveVariant, SuggestSku)
            {
                Name = variant.Name,
                Sku = variant.Sku,
                Barcode = variant.Barcode ?? string.Empty,
                StorageLabel = variant.StorageLabel ?? string.Empty,
                ColorLabel = variant.ColorLabel ?? string.Empty,
                SwatchColor = variant.SwatchColor ?? string.Empty,
                SellingPriceText = FormatMoney(variant.SellingPrice),
                CostPriceText = HasCostWriteCapability ? FormatMoney(variant.CostPrice) : string.Empty,
                AvailableQuantity = variant.AvailableQuantity,
                IsSerialTracked = product.IsSerialTracked
            };
            foreach (var specification in variant.Specifications)
            {
            row.Specifications.Add(new ProductEditSpecificationViewModel(specification.Key, specification.Value, row.RemoveSpecification));
            }

            Variants.Add(row);
        }

        RefreshVariantRemoveState();
        SelectedVariant = Variants.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelectedVariant));
        IsLoadError = false;
        IsUnavailable = false;
        NotifyDerivedState();
    }

    private void ClearLoadedProduct()
    {
        _loadedProduct = null;
        Name = Brand = CategoryId = ProductSku = Barcode = ModelId = ImageUrl = string.Empty;
        BasePriceText = CostPriceText = string.Empty;
        IsSerialTracked = false;
        AvailableQuantity = 0;
        IsPreviewData = false;
        Specifications.Clear();
        Variants.Clear();
        SelectedVariant = null;
        OnPropertyChanged(nameof(HasSelectedVariant));
        NotifyDerivedState();
    }

    private void RefreshVariantRemoveState()
    {
        var canRemove = Variants.Count > 1;
        foreach (var variant in Variants)
        {
            variant.CanRemove = canRemove;
            variant.CanEditCost = HasCostWriteCapability;
        }
    }

    private void RefreshCapabilities()
    {
        HasEditCapability = _currentSession.HasCapability(EditCapability);
        HasCostWriteCapability = _currentSession.HasCapability(CostWriteCapability);
        OnPropertyChanged(nameof(IsCostFieldsVisible));
        NotifyDerivedState();
    }

    private void OnSessionChanged(object? sender, CurrentSessionSnapshot snapshot)
    {
        RefreshCapabilities();
        if (!HasCostWriteCapability)
        {
            CostPriceText = string.Empty;
            foreach (var variant in Variants)
            {
                variant.CostPriceText = string.Empty;
                variant.CanEditCost = false;
            }
        }
    }

    private void SetError(string field, bool invalid, string message, Action<string> setError)
    {
        if (invalid)
        {
            setError(message);
            SetFirstInvalidField(field);
        }
    }

    private void SetFirstInvalidField(string field)
    {
        if (string.IsNullOrEmpty(FirstInvalidFieldName))
        {
            FirstInvalidFieldName = field;
        }
    }

    private static string NormalizeSkuPart(string value)
    {
        var normalized = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToUpperInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private void NotifyDerivedState()
    {
        OnPropertyChanged(nameof(IsPopulated));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsCostFieldsVisible));
        OnPropertyChanged(nameof(ProvenanceMessage));
        OnPropertyChanged(nameof(ImagePreviewMessage));
    }

    partial void OnSelectedVariantChanged(ProductEditVariantViewModel? value) =>
        OnPropertyChanged(nameof(HasSelectedVariant));

    private static string FormatMoney(decimal? value) => value?.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN")) ?? string.Empty;
    private static decimal? ParseOptionalMoney(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var result) ? result : null;
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed partial class ProductEditSpecificationViewModel : ObservableObject
{
    private readonly Action<ProductEditSpecificationViewModel>? _remove;
    [ObservableProperty] private string key = string.Empty;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private string error = string.Empty;

    public ProductEditSpecificationViewModel(Action<ProductEditSpecificationViewModel>? remove = null) => _remove = remove;
    public ProductEditSpecificationViewModel(string key, string value, Action<ProductEditSpecificationViewModel>? remove = null)
    {
        _remove = remove;
        (Key, Value) = (key, value);
    }

    [RelayCommand]
    public void Remove() => _remove?.Invoke(this);
}

public sealed partial class ProductEditVariantViewModel(
    string? id,
    Action<ProductEditVariantViewModel>? remove = null,
    Action<ProductEditVariantViewModel>? suggestSku = null) : ObservableObject
{
    private bool _settingSuggestedSku;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private string storageLabel = string.Empty;
    [ObservableProperty] private string colorLabel = string.Empty;
    [ObservableProperty] private string swatchColor = string.Empty;
    [ObservableProperty] private string sellingPriceText = string.Empty;
    [ObservableProperty] private string costPriceText = string.Empty;
    [ObservableProperty] private int availableQuantity;
    [ObservableProperty] private bool isSerialTracked;
    [ObservableProperty] private bool canRemove;
    [ObservableProperty] private bool canEditCost;
    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private string skuError = string.Empty;
    [ObservableProperty] private string barcodeError = string.Empty;
    [ObservableProperty] private string priceError = string.Empty;
    [ObservableProperty] private string costPriceError = string.Empty;
    [ObservableProperty] private string specificationsError = string.Empty;
    [ObservableProperty] private bool isSkuSuggestion;
    [ObservableProperty] private string skuOriginMessage = string.Empty;

    public string? Id { get; } = id;
    public ObservableCollection<ProductEditSpecificationViewModel> Specifications { get; } = new();
    public bool HasErrors => !string.IsNullOrEmpty(NameError) || !string.IsNullOrEmpty(SkuError) ||
        !string.IsNullOrEmpty(BarcodeError) || !string.IsNullOrEmpty(PriceError) ||
        !string.IsNullOrEmpty(CostPriceError) || !string.IsNullOrEmpty(SpecificationsError) ||
        Specifications.Any(item => !string.IsNullOrEmpty(item.Error));

    [RelayCommand]
    public void Remove() => remove?.Invoke(this);

    [RelayCommand]
    public void RequestSkuSuggestion() => suggestSku?.Invoke(this);

    [RelayCommand]
    public void AddSpecification() => Specifications.Add(new ProductEditSpecificationViewModel(remove: RemoveSpecification));

    [RelayCommand]
    public void RemoveSpecification(ProductEditSpecificationViewModel? item)
    {
        if (item is not null)
        {
            Specifications.Remove(item);
        }
    }

    public void SetSuggestedSku(string value)
    {
        _settingSuggestedSku = true;
        Sku = value;
        _settingSuggestedSku = false;
        IsSkuSuggestion = true;
        SkuOriginMessage = "Gợi ý tham khảo; server mới xác định SKU có hợp lệ hay không.";
    }

    partial void OnSkuChanged(string value)
    {
        if (!_settingSuggestedSku)
        {
            IsSkuSuggestion = false;
            SkuOriginMessage = string.Empty;
        }
    }

    public void ClearErrors()
    {
        NameError = SkuError = BarcodeError = PriceError = CostPriceError = SpecificationsError = string.Empty;
        foreach (var specification in Specifications)
        {
            specification.Error = string.Empty;
        }
    }
}
