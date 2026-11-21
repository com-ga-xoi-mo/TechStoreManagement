using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Inventory;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

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

public sealed partial class ReceiptLineDraft : ObservableObject
{
    private static readonly CultureInfo ViCulture = new("vi-VN");
    private static readonly char[] SerialSeparators = ['\r', '\n', ',', ';', '\t'];

    public Guid ProductId { get; }
    public string ProductName { get; }
    public string ProductSku { get; }
    public Guid? VariantId { get; }
    public string? VariantName { get; }
    public string? VariantSku { get; }
    public bool IsSerialTracked { get; }

    public const int MaxQuantity = 100_000;
    public const int MaxWarrantyMonths = 120;
    public const decimal MaxUnitCost = 1_000_000_000_000m;

    // Bound to TextBoxes with UpdateSourceTrigger.PropertyChanged, so counts and totals update while typing.
    [ObservableProperty] private string _quantityText = "1";
    [ObservableProperty] private string _unitCostText = string.Empty;
    [ObservableProperty] private string _warrantyMonthsText = "12";
    [ObservableProperty] private string _serialLines = string.Empty;

    public int Quantity
    {
        get => TryParseWholeNumber(QuantityText, out var value) ? value : 0;
        set => QuantityText = value.ToString(CultureInfo.InvariantCulture);
    }

    public int WarrantyMonths
    {
        get => TryParseWholeNumber(WarrantyMonthsText, out var value) ? value : -1;
        set => WarrantyMonthsText = value.ToString(CultureInfo.InvariantCulture);
    }

    public bool HasValidQuantity => TryParseWholeNumber(QuantityText, out var q) && q >= 1 && q <= MaxQuantity;
    public bool HasValidWarrantyMonths => TryParseWholeNumber(WarrantyMonthsText, out var w) && w >= 0 && w <= MaxWarrantyMonths;

    private static bool TryParseWholeNumber(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public string DisplayName => string.IsNullOrWhiteSpace(VariantName) ? ProductName : $"{ProductName} ({VariantName})";
    public string DisplaySku => VariantSku ?? ProductSku;

    public bool TryGetUnitCost(out decimal cost) =>
        decimal.TryParse(UnitCostText?.Trim(), NumberStyles.Number, ViCulture, out cost) && cost >= 0;

    public decimal? UnitCost => TryGetUnitCost(out var c) ? c : null;
    public decimal LineTotalPreview => (UnitCost ?? 0m) * Math.Max(0, Quantity);
    public string LineTotalFormatted => LineTotalPreview.ToString("N0", ViCulture) + " ₫";

    public List<string> ParseCodes() => SerialLines
        .Split(SerialSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(s => s.Length > 0)
        .ToList();

    internal static List<PurchaseReceiptDeviceRequest> ParseDevices(string codes) => codes
        .Split(SerialSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(code => code.Length == 15 && code.All(char.IsDigit)
            ? new PurchaseReceiptDeviceRequest { Imei = code }
            : new PurchaseReceiptDeviceRequest { SerialNumber = code }).ToList();

    public int DeviceCount => ParseCodes().Count;
    public bool HasDuplicateCodes => ParseCodes().Count != ParseCodes().Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public ReceiptLineDraft(
        Guid productId,
        string productName,
        string productSku,
        Guid? variantId,
        string? variantName,
        string? variantSku,
        bool isSerialTracked)
    {
        ProductId = productId;
        ProductName = productName;
        ProductSku = productSku;
        VariantId = variantId;
        VariantName = variantName;
        VariantSku = variantSku;
        IsSerialTracked = isSerialTracked;
    }

    partial void OnQuantityTextChanged(string value)
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(HasValidQuantity));
        OnPropertyChanged(nameof(LineTotalPreview));
        OnPropertyChanged(nameof(LineTotalFormatted));
    }

    partial void OnWarrantyMonthsTextChanged(string value)
    {
        OnPropertyChanged(nameof(WarrantyMonths));
        OnPropertyChanged(nameof(HasValidWarrantyMonths));
    }

    partial void OnUnitCostTextChanged(string value)
    {
        OnPropertyChanged(nameof(UnitCost));
        OnPropertyChanged(nameof(LineTotalPreview));
        OnPropertyChanged(nameof(LineTotalFormatted));
    }

    partial void OnSerialLinesChanged(string value)
    {
        OnPropertyChanged(nameof(DeviceCount));
        OnPropertyChanged(nameof(HasDuplicateCodes));
    }
}

public sealed partial class CreatePurchaseReceiptDialogViewModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IPurchaseReceiptRepository _receiptRepository;
    private const int ListPageSize = 100;
    private int _variantLoadGeneration;
    private CancellationTokenSource? _variantLoadCancellation;

    public Guid ClientRequestId { get; } = Guid.NewGuid();

    // True when the server answered with a receipt recorded by an earlier attempt of this form (HTTP 200 replay):
    // edits made after that attempt were not applied.
    [ObservableProperty] private bool receiveLater;
    public string SubmitLabel => ReceiveLater ? "Lập phiếu chờ nhận" : "Xác nhận nhập";

    partial void OnReceiveLaterChanged(bool value)
    {
        OnPropertyChanged(nameof(SubmitLabel));
        NotifyValidation();
    }

    public bool LastSubmitWasReplay { get; private set; }

    public ObservableCollection<SupplierItem> Suppliers { get; } = [];
    public ObservableCollection<ImportStockProductOption> ProductOptions { get; } = [];
    public ObservableCollection<ImportStockVariantOption> VariantOptions { get; } = [];
    public ObservableCollection<ReceiptLineDraft> Lines { get; } = [];

    [ObservableProperty] private SupplierItem? _selectedSupplier;
    [ObservableProperty] private string? _supplierDocumentNumber;
    [ObservableProperty] private string? _note;

    [ObservableProperty] private ImportStockProductOption? _selectedProduct;
    [ObservableProperty] private ImportStockVariantOption? _selectedVariant;
    [ObservableProperty] private bool _isLoadingProducts;
    [ObservableProperty] private bool _isLoadingVariants;
    [ObservableProperty] private bool _isLoadingSuppliers;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private IReadOnlyList<string>? _duplicateIdentifiers;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasDuplicateIdentifiers => DuplicateIdentifiers != null && DuplicateIdentifiers.Count > 0;
    public bool HasSelectedProduct => SelectedProduct is not null;
    public bool HasVariants => VariantOptions.Count > 0;
    public bool CanAddLine => SelectedProduct is not null && !IsLoadingVariants;
    public string DuplicateIdentifiersText => HasDuplicateIdentifiers
        ? "Mã bị trùng: " + string.Join(", ", DuplicateIdentifiers!)
        : string.Empty;

    public decimal TotalCostPreview => Lines.Sum(l => l.LineTotalPreview);
    public string TotalCostFormatted => TotalCostPreview.ToString("N0", ViCulture) + " ₫";
    public int TotalQuantityPreview => Lines.Sum(l => l.Quantity);

    public string? ValidationMessage
    {
        get
        {
            if (SelectedSupplier is null) return "Vui lòng chọn nhà cung cấp.";
            if (Lines.Count == 0) return "Phiếu nhập phải có ít nhất một mặt hàng.";

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in Lines)
            {
                var key = $"{line.ProductId:D}_{line.VariantId?.ToString("D") ?? "none"}";
                if (!keys.Add(key))
                {
                    return $"Sản phẩm \"{line.DisplayName}\" bị trùng lặp giữa các dòng.";
                }

                if (!line.HasValidQuantity)
                {
                    return $"Số lượng của mặt hàng \"{line.DisplayName}\" phải lớn hơn hoặc bằng 1 và không quá {ReceiptLineDraft.MaxQuantity:N0}.";
                }

                if (!line.TryGetUnitCost(out var cost) || cost < 0)
                {
                    return $"Giá nhập của mặt hàng \"{line.DisplayName}\" không hợp lệ (số không âm).";
                }

                if (decimal.Round(cost, 2) != cost) return "Giá nhập có tối đa hai chữ số thập phân.";
                if (cost > ReceiptLineDraft.MaxUnitCost)
                {
                    return $"Giá nhập của mặt hàng \"{line.DisplayName}\" không được vượt quá {ReceiptLineDraft.MaxUnitCost.ToString("N0", ViCulture)} ₫.";
                }

                if (line.IsSerialTracked)
                {
                    if (!line.HasValidWarrantyMonths)
                    {
                        return $"Thời hạn bảo hành của \"{line.DisplayName}\" phải từ 0 đến {ReceiptLineDraft.MaxWarrantyMonths} tháng.";
                    }

                    if (ReceiveLater && line.DeviceCount > 0) return "Nhập Serial/IMEI khi nhận hàng thực tế.";
                    if (ReceiveLater) continue;

                    if (line.DeviceCount != line.Quantity)
                    {
                        return $"Số mã Serial/IMEI ({line.DeviceCount}) phải bằng số lượng nhập ({line.Quantity}).";
                    }

                    if (line.HasDuplicateCodes)
                    {
                        return $"Danh sách mã của mặt hàng \"{line.DisplayName}\" có mã trùng nhau.";
                    }

                    foreach (var code in line.ParseCodes())
                    {
                        if (!allCodes.Add(code))
                        {
                            return $"Mã Serial/IMEI \"{code}\" bị trùng lặp giữa các dòng.";
                        }
                    }
                }
            }

            return null;
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool CanSubmit => !IsBusy && !HasValidationError;

    public event Action<PurchaseReceiptDetail>? ReceiptCreated;
    public event EventHandler? RequestClose;

    public CreatePurchaseReceiptDialogViewModel(
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        IPurchaseReceiptRepository receiptRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _receiptRepository = receiptRepository ?? throw new ArgumentNullException(nameof(receiptRepository));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            LoadSuppliersAsync(cancellationToken),
            LoadProductsAsync(cancellationToken));
    }

    public async Task LoadSuppliersAsync(CancellationToken cancellationToken = default)
    {
        IsLoadingSuppliers = true;
        try
        {
            var loaded = new List<SupplierItem>();
            for (var page = 1; ; page++)
            {
                var result = await _supplierRepository.GetSuppliersAsync(
                    new SupplierFilterRequest { IsActive = true, Page = page, PageSize = ListPageSize },
                    cancellationToken);
                if (!result.Succeeded)
                {
                    ErrorMessage = "Không tải được danh sách nhà cung cấp. Vui lòng đóng form và thử lại.";
                    return;
                }

                loaded.AddRange(result.Items);
                if (result.Items.Count == 0 || page >= result.TotalPages) break;
            }

            Suppliers.Clear();
            foreach (var s in loaded)
            {
                Suppliers.Add(s);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ErrorMessage = "Không tải được danh sách nhà cung cấp. Vui lòng đóng form và thử lại.";
        }
        finally
        {
            IsLoadingSuppliers = false;
        }
    }

    public async Task LoadProductsAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoadingProducts) return;
        IsLoadingProducts = true;
        try
        {
            var loaded = new List<ProductSummary>();
            for (var page = 1; ; page++)
            {
                var result = await _productRepository.SearchAsync(new ProductQuery
                {
                    IsActive = true,
                    Page = page,
                    PageSize = ListPageSize,
                    Sort = "name:asc"
                }, cancellationToken);

                loaded.AddRange(result.Items);
                if (result.Items.Count == 0 || page >= result.TotalPages) break;
            }

            ProductOptions.Clear();
            foreach (var p in loaded)
            {
                ProductOptions.Add(ImportStockProductOption.From(p));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            ErrorMessage = "Không tải được danh sách sản phẩm. Vui lòng đóng form và thử lại.";
        }
        finally
        {
            IsLoadingProducts = false;
        }
    }

    private async Task LoadVariantsAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty) return;
        var generation = ++_variantLoadGeneration;
        IsLoadingVariants = true;
        try
        {
            var detail = await _productRepository.GetByIdAsync(productId, cancellationToken);
            // The user may have picked another product while this one was loading.
            if (generation != _variantLoadGeneration || SelectedProduct?.Id != productId) return;

            VariantOptions.Clear();
            if (detail != null)
            {
                foreach (var v in detail.Variants.Where(x => x.IsActive))
                {
                    VariantOptions.Add(new ImportStockVariantOption(v.Id, v.VariantName, v.Sku));
                }
            }
            OnPropertyChanged(nameof(HasVariants));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Another product was picked; its own load reports the result.
        }
        catch (Exception)
        {
            if (generation == _variantLoadGeneration)
            {
                ErrorMessage = "Không tải được biến thể của sản phẩm. Vui lòng chọn lại sản phẩm.";
            }
        }
        finally
        {
            if (generation == _variantLoadGeneration)
            {
                IsLoadingVariants = false;
            }
        }
    }

    partial void OnSelectedProductChanged(ImportStockProductOption? value)
    {
        SelectedVariant = null;
        VariantOptions.Clear();
        _variantLoadCancellation?.Cancel();
        _variantLoadCancellation = null;
        if (value != null && !value.VariantId.HasValue)
        {
            _variantLoadCancellation = new CancellationTokenSource();
            _ = LoadVariantsAsync(value.Id, _variantLoadCancellation.Token);
        }
        else
        {
            _variantLoadGeneration++;
            IsLoadingVariants = false;
        }

        OnPropertyChanged(nameof(HasSelectedProduct));
        OnPropertyChanged(nameof(HasVariants));
        OnPropertyChanged(nameof(CanAddLine));
        AddLineCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSupplierChanged(SupplierItem? value) => NotifyValidation();
    partial void OnIsBusyChanged(bool value) => NotifyValidation();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnDuplicateIdentifiersChanged(IReadOnlyList<string>? value)
    {
        OnPropertyChanged(nameof(HasDuplicateIdentifiers));
        OnPropertyChanged(nameof(DuplicateIdentifiersText));
    }

    partial void OnIsLoadingVariantsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAddLine));
        AddLineCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    public void AddLine()
    {
        if (!CanAddLine || SelectedProduct is null) return;
        if (HasVariants && SelectedVariant is null)
        {
            ErrorMessage = "Vui lòng chọn biến thể cho sản phẩm này.";
            return;
        }

        var productId = SelectedProduct.Id;
        var variantId = SelectedVariant?.Id;

        // Check if already in lines
        var exists = Lines.Any(l => l.ProductId == productId && l.VariantId == variantId);
        if (exists)
        {
            ErrorMessage = $"Sản phẩm \"{SelectedProduct.Name}\" đã có trong danh sách dòng.";
            return;
        }

        ErrorMessage = null;
        var draft = new ReceiptLineDraft(
            productId,
            SelectedProduct.Name,
            SelectedProduct.Sku,
            variantId,
            SelectedVariant?.Name,
            SelectedVariant?.Sku,
            SelectedProduct.IsSerialTracked);

        draft.PropertyChanged += OnLineDraftPropertyChanged;
        Lines.Add(draft);

        SelectedProduct = null;
        SelectedVariant = null;
        VariantOptions.Clear();

        NotifyTotalsAndValidation();
    }

    [RelayCommand]
    public void RemoveLine(object? parameter)
    {
        if (parameter is ReceiptLineDraft draft)
        {
            draft.PropertyChanged -= OnLineDraftPropertyChanged;
            Lines.Remove(draft);
            NotifyTotalsAndValidation();
        }
    }

    private void OnLineDraftPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyTotalsAndValidation();
    }

    private void NotifyTotalsAndValidation()
    {
        OnPropertyChanged(nameof(TotalCostPreview));
        OnPropertyChanged(nameof(TotalCostFormatted));
        OnPropertyChanged(nameof(TotalQuantityPreview));
        NotifyValidation();
    }

    private void NotifyValidation()
    {
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSubmit || IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        DuplicateIdentifiers = null;

        try
        {
            var request = new CreatePurchaseReceiptRequest
            {
                ReceiveLater = ReceiveLater,
                ClientRequestId = ClientRequestId,
                SupplierId = SelectedSupplier!.Id,
                SupplierDocumentNumber = string.IsNullOrWhiteSpace(SupplierDocumentNumber) ? null : SupplierDocumentNumber.Trim(),
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                Lines = Lines.Select(l => new PurchaseReceiptLineRequest
                {
                    ProductId = l.ProductId,
                    VariantId = l.VariantId,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost!.Value,
                    WarrantyMonths = l.IsSerialTracked ? l.WarrantyMonths : 0,
                    Devices = l.IsSerialTracked && !ReceiveLater ? ReceiptLineDraft.ParseDevices(l.SerialLines) : []
                }).ToList()
            };

            var result = await _receiptRepository.CreateAsync(request, cancellationToken);
            if (result.Succeeded && result.Data != null)
            {
                LastSubmitWasReplay = result.IsReplay;
                ReceiptCreated?.Invoke(result.Data);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = DescribeFailure(result);
                if (result.Failure == ReceiptFailureKind.Conflict)
                {
                    DuplicateIdentifiers = result.DuplicateIdentifiers;
                }
            }
        }
        catch (Exception)
        {
            ErrorMessage = NetworkFailureMessage;
        }
        finally
        {
            IsBusy = false;
            NotifyValidation();
        }
    }

    internal const string NetworkFailureMessage =
        "Không kết nối được máy chủ. Các giá trị đã nhập vẫn được giữ; bấm \"Xác nhận nhập\" lần nữa để thử lại, phiếu sẽ không bị ghi hai lần.";

    private string DescribeFailure(PurchaseReceiptCreateResult result) => result.Failure switch
    {
        ReceiptFailureKind.BadRequest => DescribeFieldErrors(result.FieldErrors)
            ?? "Dữ liệu phiếu nhập không hợp lệ. Vui lòng kiểm tra lại.",
        ReceiptFailureKind.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        ReceiptFailureKind.Forbidden => "Bạn không có quyền tạo phiếu nhập.",
        ReceiptFailureKind.NotFound => "Không tìm thấy dữ liệu liên quan đến phiếu nhập.",
        ReceiptFailureKind.Conflict => result.Message ?? "Xung đột dữ liệu phiếu nhập. Vui lòng thử lại.",
        _ => NetworkFailureMessage
    };

    // Turns server keys such as "lines[0].variantId" into "Dòng 1 (iPhone 16): ...".
    private string? DescribeFieldErrors(IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors)
    {
        if (fieldErrors is null || fieldErrors.Count == 0) return null;

        var messages = new List<string>();
        foreach (var (key, errors) in fieldErrors)
        {
            var prefix = DescribeField(key);
            foreach (var error in errors)
            {
                messages.Add(prefix is null ? error : $"{prefix}: {error}");
            }
        }

        return messages.Count == 0 ? null : string.Join(Environment.NewLine, messages.Distinct());
    }

    private string? DescribeField(string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            key, @"^lines\[(\d+)\](?:\.devices\[(\d+)\])?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var lineIndex))
        {
            var lineName = lineIndex >= 0 && lineIndex < Lines.Count ? $" ({Lines[lineIndex].DisplayName})" : string.Empty;
            var device = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var deviceIndex)
                ? $", mã thứ {deviceIndex + 1}"
                : string.Empty;
            return $"Dòng {lineIndex + 1}{lineName}{device}";
        }

        return key.ToLowerInvariant() switch
        {
            "supplierid" => "Nhà cung cấp",
            "supplierdocumentnumber" => "Số chứng từ",
            "note" => "Ghi chú",
            "lines" => "Danh sách mặt hàng",
            _ => null
        };
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
