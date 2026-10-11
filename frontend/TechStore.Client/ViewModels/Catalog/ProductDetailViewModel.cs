using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductDetailViewModel : ObservableObject, IDisposable
{
    private readonly IProductRepository _productRepository;
    private readonly ICurrentSession _currentSession;
    private CancellationTokenSource? _loadCancellation;
    private long _loadGeneration;
    private ProductDetail? _loadedDetail;
    private string? _selectedVariantId;

    [ObservableProperty]
    private string? productId;

    [ObservableProperty]
    private ProductDetail? detail;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private bool isUnavailable;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? currentSku;

    [ObservableProperty]
    private decimal? currentSellingPrice;

    [ObservableProperty]
    private int? currentAvailableQuantity;

    [ObservableProperty]
    private ProductStatus? currentStatus;

    [ObservableProperty]
    private decimal? averageCost;

    [ObservableProperty]
    private decimal? estimatedMargin;

    [ObservableProperty]
    private bool hasCostReadCapability;

    public ProductDetailVariant? SelectedVariant { get; private set; }

    public string? SelectedVariantId
    {
        get => _selectedVariantId;
        set => SelectVariant(value);
    }

    public bool IsVariantSelectorVisible => Detail?.Variants.Count > 1;

    public string ProductName => Detail?.Name ?? string.Empty;

    public string ProductBrand => Detail?.Brand ?? string.Empty;

    public string ProductModelId => Detail?.ModelId is { Length: > 0 } modelId ? $"Mã sản phẩm: {modelId}" : string.Empty;

    public string WarrantyText => Detail?.WarrantyMonths is int months ? $"Bảo hành: {months} tháng" : string.Empty;

    public string? ProductImageUrl => Detail?.ImageUrl;

    public ProductStatus? ProductStatus => Detail?.Status;

    public bool IsPreviewData => Detail?.IsPreviewData == true;

    public System.Collections.Generic.IReadOnlyList<ProductDetailVariant> AvailableVariants =>
        Detail?.Variants ?? Array.Empty<ProductDetailVariant>();

    public bool IsCostPanelVisible => HasCostReadCapability && (AverageCost.HasValue || EstimatedMargin.HasValue);

    public System.Collections.Generic.IReadOnlyList<ProductSpecification> CurrentSpecifications =>
        Detail is null
            ? Array.Empty<ProductSpecification>()
            : SelectedVariant is null
                ? Detail.Specifications
                : Detail.Specifications.Concat(SelectedVariant.Specifications).ToArray();

    public bool IsPopulated => Detail is not null && !IsLoading && !IsError;

    public ProductDetailViewModel(IProductRepository productRepository, ICurrentSession currentSession)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        HasCostReadCapability = _currentSession.HasCapability(CostReadCapability);
        _currentSession.SessionChanged += OnSessionChanged;
    }

    public const string CostReadCapability = "catalog:cost-read";

    public bool SelectVariant(string? variantId)
    {
        if (string.IsNullOrWhiteSpace(variantId) || Detail is null)
        {
            return false;
        }

        var variant = Detail.Variants.FirstOrDefault(item => item.Id == variantId);
        if (variant is null)
        {
            return false;
        }

        _selectedVariantId = variant.Id;
        SelectedVariant = variant;
        CurrentSku = variant.Sku;
        CurrentSellingPrice = variant.SellingPrice;
        CurrentAvailableQuantity = variant.AvailableQuantity;
        CurrentStatus = variant.Status;
        AverageCost = HasCostReadCapability ? variant.AverageCost : null;
        EstimatedMargin = HasCostReadCapability ? variant.EstimatedMargin : null;
        OnPropertyChanged(nameof(SelectedVariantId));
        OnPropertyChanged(nameof(SelectedVariant));
        OnPropertyChanged(nameof(CurrentSpecifications));
        OnPropertyChanged(nameof(IsCostPanelVisible));
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

    public async Task LoadAsync(string productId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        var generation = Interlocked.Increment(ref _loadGeneration);
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;

        ProductId = productId;
        _loadedDetail = null;
        Detail = null;
        ClearSelectedVariant();
        IsLoading = true;
        IsError = false;
        IsUnavailable = false;
        ErrorMessage = null;

        try
        {
            var result = await _productRepository.GetProductDetailAsync(productId, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!IsCurrent(generation))
            {
                return;
            }

            _loadedDetail = result;
            SetVisibleDetail(result, selectedVariantId: null);
            IsUnavailable = result is null;
        }
        catch (OperationCanceledException)
        {
            // A newer product request or dialog close superseded this result.
        }
        catch (Exception)
        {
            if (IsCurrent(generation))
            {
                Detail = null;
                _loadedDetail = null;
                ClearSelectedVariant();
                IsError = true;
                IsUnavailable = false;
                ErrorMessage = "Không thể tải chi tiết sản phẩm. Vui lòng thử lại.";
            }
        }
        finally
        {
            if (IsCurrent(generation))
            {
                IsLoading = false;
            }

            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    public void CancelLoad()
    {
        Interlocked.Increment(ref _loadGeneration);
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    public void Dispose()
    {
        CancelLoad();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _currentSession.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, CurrentSessionSnapshot snapshot)
    {
        var selectedId = _selectedVariantId;
        HasCostReadCapability = _currentSession.HasCapability(CostReadCapability);
        SetVisibleDetail(_loadedDetail, selectedId);
        OnPropertyChanged(nameof(IsCostPanelVisible));
    }

    private void SetVisibleDetail(ProductDetail? value, string? selectedVariantId)
    {
        ClearSelectedVariant();
        if (value is null)
        {
            Detail = null;
            return;
        }

        Detail = HasCostReadCapability
            ? value
            : value with
            {
                Variants = value.Variants
                    .Select(variant => variant with { AverageCost = null, EstimatedMargin = null })
                    .ToArray()
            };

        OnPropertyChanged(nameof(IsVariantSelectorVisible));
        OnPropertyChanged(nameof(CurrentSpecifications));
        var initialVariant = Detail.Variants.FirstOrDefault(variant => variant.Id == selectedVariantId)
            ?? Detail.Variants.FirstOrDefault();
        if (initialVariant is not null)
        {
            SelectVariant(initialVariant.Id);
        }
    }

    private void ClearSelectedVariant()
    {
        _selectedVariantId = null;
        SelectedVariant = null;
        CurrentSku = null;
        CurrentSellingPrice = null;
        CurrentAvailableQuantity = null;
        CurrentStatus = null;
        AverageCost = null;
        EstimatedMargin = null;
        OnPropertyChanged(nameof(SelectedVariantId));
        OnPropertyChanged(nameof(SelectedVariant));
        OnPropertyChanged(nameof(CurrentSpecifications));
        OnPropertyChanged(nameof(IsCostPanelVisible));
    }

    private bool IsCurrent(long generation) => generation == Volatile.Read(ref _loadGeneration);

    partial void OnDetailChanged(ProductDetail? value)
    {
        OnPropertyChanged(nameof(ProductName));
        OnPropertyChanged(nameof(ProductBrand));
        OnPropertyChanged(nameof(ProductModelId));
        OnPropertyChanged(nameof(WarrantyText));
        OnPropertyChanged(nameof(ProductImageUrl));
        OnPropertyChanged(nameof(ProductStatus));
        OnPropertyChanged(nameof(IsPreviewData));
        OnPropertyChanged(nameof(AvailableVariants));
        OnPropertyChanged(nameof(IsPopulated));
        OnPropertyChanged(nameof(IsVariantSelectorVisible));
        OnPropertyChanged(nameof(CurrentSpecifications));
    }
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsPopulated));
    partial void OnIsErrorChanged(bool value) => OnPropertyChanged(nameof(IsPopulated));

    partial void OnHasCostReadCapabilityChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCostPanelVisible));
    }

    partial void OnAverageCostChanged(decimal? value) => OnPropertyChanged(nameof(IsCostPanelVisible));
    partial void OnEstimatedMarginChanged(decimal? value) => OnPropertyChanged(nameof(IsCostPanelVisible));
}
