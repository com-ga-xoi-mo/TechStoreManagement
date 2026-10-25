using TechStore.Client.Repositories.Inventory;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Shared.Requests;

namespace TechStore.Client.Features.Inventory.ViewModels;

public sealed record ImportStockProductOption(
    Guid Id,
    string Name,
    string Sku,
    string CategoryName,
    bool IsSerialTracked,
    Guid? VariantId = null,
    string? VariantName = null,
    string? VariantSku = null)
{
    public string DisplayText => VariantId.HasValue
        ? $"{Name} · {VariantName} · {VariantSku}"
        : $"{Name} · {Sku}";
    public string MetaText => VariantId.HasValue
        ? $"{VariantSku} · {CategoryName}"
        : $"{Sku} · {CategoryName}";

    public static ImportStockProductOption From(ProductSummary product) => new(
        product.Id,
        product.Name,
        product.Sku,
        product.CategoryName,
        product.IsSerialTracked);
}

public sealed record ImportStockVariantOption(Guid Id, string Name, string Sku)
{
    public string DisplayText => $"{Name} · {Sku}";
}

public sealed partial class ImportStockDialogViewModel(
    IProductRepository productRepository,
    IInventoryRepository inventoryApiClient) : ObservableObject
{
    private readonly IProductRepository _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    private readonly IInventoryRepository _inventoryApiClient = inventoryApiClient ?? throw new ArgumentNullException(nameof(inventoryApiClient));

    public ObservableCollection<ImportStockProductOption> ProductOptions { get; } = [];
    public ObservableCollection<ImportStockVariantOption> VariantOptions { get; } = [];

    [ObservableProperty] private ImportStockProductOption? _selectedProduct;
    [ObservableProperty] private ImportStockVariantOption? _selectedVariant;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private string _serialLines = string.Empty;
    [ObservableProperty] private string _reason = "Nhập kho mới";
    [ObservableProperty] private bool _isLoadingProducts;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoadingVariants;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSelectedProduct => SelectedProduct is not null;
    public bool IsSerialTracked => SelectedProduct?.IsSerialTracked == true;
    public bool HasVariants => VariantOptions.Count > 0;
    public int SerialCount => ParseSerialEntries().Count;
    public string SerialCountText => $"Đã nhập {SerialCount:N0} mã Serial/IMEI";
    public string ProductMetaText => SelectedProduct is null
        ? "Chọn sản phẩm hoặc SKU để bắt đầu"
        : SelectedVariant is null ? SelectedProduct.MetaText : $"{SelectedVariant.Sku} · {SelectedProduct.CategoryName}";
    public string? ValidationMessage
    {
        get
        {
            if (SelectedProduct is null) return "Vui lòng chọn sản phẩm hoặc SKU.";
            if (Quantity <= 0) return "Số lượng nhập kho phải lớn hơn hoặc bằng 1.";
            if (HasDuplicateSerials) return "Danh sách Serial/IMEI không được có mã trùng nhau.";
            if (SerialCount > 0 && SerialCount != Quantity)
                return $"Số mã Serial/IMEI ({SerialCount}) phải bằng số lượng nhập ({Quantity}).";
            return null;
        }
    }
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool HasDuplicateSerials => ParseSerialEntries().Count != ParseSerialLines().Count;
    public bool CanSubmit => !IsBusy && ValidationMessage is null;

    public event EventHandler? RequestClose;
    public event Action<int>? ImportSucceeded;

    partial void OnSelectedProductChanged(ImportStockProductOption? value)
    {
        VariantOptions.Clear();
        SelectedVariant = null;
        OnPropertyChanged(nameof(HasVariants));
        if (value is not null && !value.VariantId.HasValue)
            _ = LoadVariantsAsync(value.Id);
        OnPropertyChanged(nameof(HasSelectedProduct));
        OnPropertyChanged(nameof(IsSerialTracked));
        OnPropertyChanged(nameof(ProductMetaText));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
        NotifyValidation();
    }

    partial void OnSelectedVariantChanged(ImportStockVariantOption? value)
    {
        OnPropertyChanged(nameof(ProductMetaText));
        OnPropertyChanged(nameof(ValidationMessage));
        NotifyValidation();
    }

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
        NotifyValidation();
    }

    partial void OnSerialLinesChanged(string value)
    {
        OnPropertyChanged(nameof(SerialCount));
        OnPropertyChanged(nameof(SerialCountText));
        OnPropertyChanged(nameof(HasDuplicateSerials));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
        NotifyValidation();
    }

    partial void OnReasonChanged(string value) => NotifyValidation();
    partial void OnIsBusyChanged(bool value) => NotifyValidation();

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void NotifyValidation() => OnPropertyChanged(nameof(CanSubmit));

    [RelayCommand]
    private async Task LoadProductsAsync(CancellationToken cancellationToken)
    {
        if (IsLoadingProducts) return;
        IsLoadingProducts = true;
        ErrorMessage = null;
        try
        {
            var page = await _productRepository.SearchAsync(new ProductQuery
            {
                IsActive = true,
                Page = 1,
                PageSize = 100,
                Sort = "name:asc"
            }, cancellationToken);

            ProductOptions.Clear();
            foreach (var product in page.Items)
            {
                ProductOptions.Add(ImportStockProductOption.From(product));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoadingProducts = false;
        }
    }

    private async Task LoadVariantsAsync(Guid productId)
    {
        if (productId == Guid.Empty) return;
        IsLoadingVariants = true;
        try
        {
            var detail = await _productRepository.GetByIdAsync(productId);
            VariantOptions.Clear();
            if (detail is not null)
            {
                foreach (var variant in detail.Variants.Where(v => v.IsActive))
                    VariantOptions.Add(new ImportStockVariantOption(variant.Id, variant.VariantName, variant.Sku));
            }
            OnPropertyChanged(nameof(HasVariants));
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoadingVariants = false;
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (!CanSubmit || SelectedProduct is null) return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var serials = ParseSerialLines();
            if (serials.Count > 0)
            {
                var existing = await _inventoryApiClient.GetStocksAsync(new InventoryStockFilterRequest
                {
                    ProductId = SelectedProduct.Id,
                    VariantId = SelectedVariant?.Id,
                    Page = 1,
                    PageSize = 1
                }, cancellationToken);

                if (existing.TotalCount > 0)
                {
                    ErrorMessage = "Sản phẩm này đã được khởi tạo tồn kho. Không thể nhập tồn ban đầu trùng lặp.";
                    return;
                }

                var result = await _inventoryApiClient.BatchImportSerialsAsync(
                    new BatchImportSerialImeiRequest
                    {
                        ProductId = SelectedProduct.Id,
                        VariantId = SelectedVariant?.Id,
                        Items = serials.Select(ToImportItem).ToList(),
                        Reason = NormalizedReason()
                    }, cancellationToken);

                if (!result.Succeeded)
                {
                    ErrorMessage = FriendlyError(result.Failure, result.Message);
                    return;
                }

                ImportSucceeded?.Invoke(result.ImportedCount);
                RequestClose?.Invoke(this, EventArgs.Empty);
                return;
            }

            var initialize = await _inventoryApiClient.InitializeStockAsync(
                new InitializeStockRequest
                {
                    ProductId = SelectedProduct.Id,
                    VariantId = SelectedVariant?.Id,
                    Quantity = Quantity,
                    Reason = NormalizedReason()
                }, cancellationToken);

            if (!initialize.Succeeded)
            {
                ErrorMessage = FriendlyError(initialize.Failure, initialize.Message);
                return;
            }

            ImportSucceeded?.Invoke(initialize.Data?.Quantity ?? Quantity);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    private string NormalizedReason() => string.IsNullOrWhiteSpace(Reason) ? "Nhập kho mới" : Reason.Trim();

    private static SerialImeiImportItemRequest ToImportItem(string value) =>
        value.Length == 15 && value.All(char.IsDigit)
            ? new SerialImeiImportItemRequest { Imei = value }
            : new SerialImeiImportItemRequest { SerialNumber = value };

    private List<string> ParseSerialEntries() => SerialLines
        .Split(['\r', '\n', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => value.Length > 0)
        .ToList();

    private List<string> ParseSerialLines() => ParseSerialEntries()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string FriendlyError(InventoryStockDetailFailure failure, string? message) => failure switch
    {
        InventoryStockDetailFailure.Conflict => "Sản phẩm này đã tồn tại trong kho hoặc Serial/IMEI đã được sử dụng.",
        InventoryStockDetailFailure.Forbidden => "Bạn không có quyền nhập kho.",
        InventoryStockDetailFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        InventoryStockDetailFailure.BadRequest => message ?? "Dữ liệu nhập kho chưa hợp lệ.",
        InventoryStockDetailFailure.NotFound => "Không tìm thấy sản phẩm cần nhập kho.",
        _ => message ?? "Không thể khởi tạo tồn kho. Vui lòng thử lại."
    };
}
