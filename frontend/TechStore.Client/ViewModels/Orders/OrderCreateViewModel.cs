using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Orders;

/// <summary>
/// Builds an order at the counter. All amounts shown here are estimates; the server prices the real order
/// and its answer is the number the user finally sees.
/// </summary>
public sealed partial class OrderCreateViewModel : PageViewModel
{
    public const int MaxLines = 100;
    public const int MaxQuantity = 10_000;
    public const int MaxNotesLength = 500;
    private const int ProductResultSize = 10;

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Regex LineErrorKey = new(@"^items\[(\d+)\]\.?(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IOrderRepository _orders;
    private readonly IOrderDeviceRepository _devices;
    private readonly IProductRepository _products;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private int _productSearchVersion;
    private int _productDetailVersion;
    private int _deviceVersion;
    private OrderDetail? _createdOrder;
    private IReadOnlyList<AvailableDevice> _allDevices = [];
    private int _deviceTotal;

    // ---- line editor ----
    [ObservableProperty] private string productSearchText = string.Empty;
    [ObservableProperty] private IReadOnlyList<ProductChoice> productResults = [];
    [ObservableProperty] private string productSearchMessage = string.Empty;
    [ObservableProperty] private bool isSearchingProducts;
    [ObservableProperty] private bool isLoadingProduct;
    [ObservableProperty] private ProductDetail? selectedProduct;
    [ObservableProperty] private IReadOnlyList<ProductVariant> variants = [];
    [ObservableProperty] private ProductVariant? selectedVariant;
    [ObservableProperty] private IReadOnlyList<AvailableDevice> devices = [];
    [ObservableProperty] private AvailableDevice? selectedDevice;
    [ObservableProperty] private string deviceNotice = string.Empty;
    [ObservableProperty] private bool isLoadingDevices;
    [ObservableProperty] private string quantityText = "1";
    [ObservableProperty] private string discountText = string.Empty;
    [ObservableProperty] private string lineError = string.Empty;

    // ---- the draft ----
    [ObservableProperty] private IReadOnlyList<DraftLineItem> lines = [];
    [ObservableProperty] private PaymentOption selectedPayment = PaymentOption.All[0];
    [ObservableProperty] private string notes = string.Empty;

    // ---- submission ----
    [ObservableProperty] private bool isSubmitting;
    [ObservableProperty] private string generalError = string.Empty;
    [ObservableProperty] private string partialFailureMessage = string.Empty;

    public OrderCreateViewModel(
        IOrderRepository orders,
        IOrderDeviceRepository devices,
        IProductRepository products,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public IReadOnlyList<PaymentOption> PaymentOptions => PaymentOption.All;

    public bool HasProduct => SelectedProduct is not null;
    public string SelectedProductName => SelectedProduct?.Name ?? string.Empty;
    public bool IsSerialTracked => SelectedProduct?.IsSerialTracked == true;
    public bool ShowVariantPicker => Variants.Count > 0;
    public bool ShowDevicePicker => HasProduct && IsSerialTracked && (!ShowVariantPicker || SelectedVariant is not null);
    public bool ShowQuantity => HasProduct && !IsSerialTracked;
    public bool HasProductResults => ProductResults.Count > 0;
    public bool HasProductSearchMessage => !string.IsNullOrWhiteSpace(ProductSearchMessage);
    public bool HasDeviceNotice => !string.IsNullOrWhiteSpace(DeviceNotice);
    public bool HasLineError => !string.IsNullOrWhiteSpace(LineError);
    public bool HasLines => Lines.Count > 0;
    public bool HasGeneralError => !string.IsNullOrWhiteSpace(GeneralError);
    public bool HasPartialFailure => !string.IsNullOrWhiteSpace(PartialFailureMessage);
    public decimal EstimatedTotal => Lines.Sum(item => item.Line.EstimatedTotal);
    public string EstimatedTotalText => OrderPresentation.FormatMoney(EstimatedTotal);
    public string LineCountText => Lines.Count == 1 ? "1 dòng" : $"{Lines.Count} dòng";
    public bool CanSubmit => Lines.Count > 0 && !IsSubmitting && _createdOrder is null;
    public string? CreatedOrderCode => _createdOrder?.OrderCode;

    protected override bool ManagesLoadState => true;

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        ContentState = PageContentState.Content;
        return Task.FromResult(true);
    }

    // ---------- reactions that keep derived state in sync ----------

    partial void OnSelectedProductChanged(ProductDetail? value)
    {
        OnPropertyChanged(nameof(HasProduct));
        OnPropertyChanged(nameof(SelectedProductName));
        OnPropertyChanged(nameof(IsSerialTracked));
        OnPropertyChanged(nameof(ShowDevicePicker));
        OnPropertyChanged(nameof(ShowQuantity));
    }

    partial void OnVariantsChanged(IReadOnlyList<ProductVariant> value)
    {
        OnPropertyChanged(nameof(ShowVariantPicker));
        OnPropertyChanged(nameof(ShowDevicePicker));
    }

    partial void OnSelectedVariantChanged(ProductVariant? value)
    {
        OnPropertyChanged(nameof(ShowDevicePicker));
        if (SelectedProduct is { IsSerialTracked: true } && value is not null)
            _ = LoadDevicesAsync(SelectedProduct.Id, value.Id);
    }

    partial void OnProductResultsChanged(IReadOnlyList<ProductChoice> value) => OnPropertyChanged(nameof(HasProductResults));

    partial void OnProductSearchMessageChanged(string value) => OnPropertyChanged(nameof(HasProductSearchMessage));

    partial void OnDeviceNoticeChanged(string value) => OnPropertyChanged(nameof(HasDeviceNotice));

    partial void OnLineErrorChanged(string value) => OnPropertyChanged(nameof(HasLineError));

    partial void OnGeneralErrorChanged(string value) => OnPropertyChanged(nameof(HasGeneralError));

    partial void OnPartialFailureMessageChanged(string value) => OnPropertyChanged(nameof(HasPartialFailure));

    partial void OnIsSubmittingChanged(bool value) => RefreshSubmitState();

    partial void OnLinesChanged(IReadOnlyList<DraftLineItem> value)
    {
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(EstimatedTotal));
        OnPropertyChanged(nameof(EstimatedTotalText));
        OnPropertyChanged(nameof(LineCountText));
        RefreshSubmitState();
    }

    private void RefreshSubmitState()
    {
        OnPropertyChanged(nameof(CanSubmit));
        SaveDraftCommand.NotifyCanExecuteChanged();
        PayAndCompleteCommand.NotifyCanExecuteChanged();
    }

    // ---------- finding a product ----------

    [RelayCommand]
    private async Task SearchProducts()
    {
        var version = ++_productSearchVersion;
        IsSearchingProducts = true;
        ProductSearchMessage = string.Empty;
        try
        {
            var query = new ProductQuery
            {
                Query = string.IsNullOrWhiteSpace(ProductSearchText) ? null : ProductSearchText.Trim(),
                IsActive = true,
                Sort = "name:asc",
                PageSize = ProductResultSize
            };
            var page = await _products.SearchAsync(query, ActivationToken);
            if (version != _productSearchVersion) return;

            ProductResults = page.Items.Select(ProductChoice.From).ToArray();
            if (page.Items.Count == 0)
                ProductSearchMessage = "Không tìm thấy sản phẩm đang kinh doanh phù hợp.";
            else if (page.TotalCount > page.Items.Count)
                ProductSearchMessage = $"Hiển thị {page.Items.Count} / {page.TotalCount} sản phẩm. Hãy nhập thêm từ khóa để thu hẹp.";
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException)
        {
            if (version != _productSearchVersion) return;
            ProductResults = [];
            ProductSearchMessage = "Không thể tải danh sách sản phẩm. Hãy thử lại.";
        }
        finally
        {
            if (version == _productSearchVersion) IsSearchingProducts = false;
        }
    }

    [RelayCommand]
    private void ClearProductSearch()
    {
        Interlocked.Increment(ref _productSearchVersion);
        ProductSearchText = string.Empty;
        ProductResults = [];
        ProductSearchMessage = string.Empty;
        IsSearchingProducts = false;
    }

    [RelayCommand]
    private async Task SelectProduct(ProductSummary? summary)
    {
        if (summary is null || summary.Id == Guid.Empty) return;

        var version = ++_productDetailVersion;
        ResetEditor();
        IsLoadingProduct = true;
        try
        {
            var detail = await _products.GetByIdAsync(summary.Id, ActivationToken);
            if (version != _productDetailVersion) return;

            if (detail is null)
            {
                LineError = "Sản phẩm không còn tồn tại.";
                return;
            }

            if (!detail.IsActive)
            {
                LineError = "Sản phẩm đã ngừng kinh doanh.";
                return;
            }

            var activeVariants = detail.Variants.Where(variant => variant.IsActive).ToArray();
            if (detail.Variants.Count > 0 && activeVariants.Length == 0)
            {
                LineError = "Sản phẩm không có biến thể nào đang hoạt động nên chưa bán được.";
                return;
            }

            SelectedProduct = detail;
            Variants = activeVariants;
            if (activeVariants.Length == 1) SelectedVariant = activeVariants[0];
            else if (activeVariants.Length == 0 && detail.IsSerialTracked) _ = LoadDevicesAsync(detail.Id, null);
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException)
        {
            if (version == _productDetailVersion) LineError = "Không thể tải chi tiết sản phẩm. Hãy thử lại.";
        }
        finally
        {
            if (version == _productDetailVersion) IsLoadingProduct = false;
        }
    }

    private async Task LoadDevicesAsync(Guid productId, Guid? variantId)
    {
        var version = ++_deviceVersion;
        IsLoadingDevices = true;
        DeviceNotice = string.Empty;
        Devices = [];
        SelectedDevice = null;
        try
        {
            var page = await _devices.GetInStockAsync(productId, variantId, ActivationToken);
            if (version != _deviceVersion) return;

            _allDevices = page.Items;
            _deviceTotal = page.TotalCount;
            RefreshDevices();
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException)
        {
            if (version == _deviceVersion) DeviceNotice = "Không thể tải danh sách máy trong kho. Hãy chọn lại biến thể để thử lại.";
        }
        finally
        {
            if (version == _deviceVersion) IsLoadingDevices = false;
        }
    }

    /// <summary>Offers the InStock devices that are not already in the draft, and explains an empty or capped list.</summary>
    private void RefreshDevices()
    {
        if (SelectedProduct is not { IsSerialTracked: true }) return;

        var used = Lines.Where(item => item.Line.SerialImeiId.HasValue).Select(item => item.Line.SerialImeiId!.Value).ToHashSet();
        Devices = _allDevices.Where(device => !used.Contains(device.Id)).ToArray();
        if (SelectedDevice is not null && !Devices.Contains(SelectedDevice)) SelectedDevice = null;

        DeviceNotice = _allDevices.Count == 0 ? "Không còn máy nào trong kho cho lựa chọn này."
            : _deviceTotal > _allDevices.Count ? $"Chỉ hiển thị {_allDevices.Count} / {_deviceTotal} máy đầu tiên."
            : Devices.Count == 0 ? "Tất cả máy còn trong kho đã được thêm vào đơn."
            : string.Empty;
    }

    // ---------- the draft ----------

    [RelayCommand]
    private void AddLine()
    {
        LineError = string.Empty;
        if (_createdOrder is not null)
        {
            LineError = $"Đơn {_createdOrder.OrderCode} đã được tạo. Hãy về danh sách đơn để xử lý tiếp.";
            return;
        }

        if (!TryBuildLine(out var line, out var error))
        {
            LineError = error;
            return;
        }

        var items = Lines.ToList();
        var mergeIndex = line.IsSerialTracked ? -1 : items.FindIndex(item => IsSameNonTrackedLine(item.Line, line));
        if (mergeIndex >= 0)
        {
            var existing = items[mergeIndex].Line;
            var merged = existing with
            {
                Quantity = existing.Quantity + line.Quantity,
                DiscountAmount = existing.DiscountAmount + line.DiscountAmount
            };
            if (merged.Quantity > MaxQuantity)
            {
                LineError = $"Tổng số lượng của một sản phẩm tối đa {MaxQuantity:N0}.";
                return;
            }

            if (merged.DiscountAmount > merged.GrossAmount)
            {
                LineError = "Tổng giảm giá không được lớn hơn thành tiền của dòng.";
                return;
            }

            items[mergeIndex] = new DraftLineItem(merged);
        }
        else
        {
            items.Add(new DraftLineItem(line));
        }

        Lines = items;
        ResetEditor();
    }

    [RelayCommand]
    private void RemoveLine(DraftLineItem? item)
    {
        if (item is null || _createdOrder is not null || !Lines.Any(candidate => ReferenceEquals(candidate, item))) return;
        Lines = Lines.Where(candidate => !ReferenceEquals(candidate, item)).ToArray();
        GeneralError = string.Empty;
        RefreshDevices();
    }

    private bool TryBuildLine(out OrderDraftLine line, out string error)
    {
        line = default!;
        error = string.Empty;

        if (Lines.Count >= MaxLines)
        {
            error = $"Một đơn tối đa {MaxLines} dòng.";
            return false;
        }

        if (SelectedProduct is not { } product)
        {
            error = "Hãy tìm và chọn một sản phẩm.";
            return false;
        }

        if (Variants.Count > 0 && SelectedVariant is null)
        {
            error = "Hãy chọn biến thể.";
            return false;
        }

        var variant = Variants.Count > 0 ? SelectedVariant : null;
        var quantity = 1;
        AvailableDevice? device = null;
        if (product.IsSerialTracked)
        {
            device = SelectedDevice;
            if (device is null)
            {
                error = "Hãy chọn một máy (IMEI/serial) còn trong kho.";
                return false;
            }

            if (Lines.Any(item => item.Line.SerialImeiId == device.Id))
            {
                error = "Máy này đã có trong đơn.";
                return false;
            }
        }
        else if (!int.TryParse(QuantityText?.Trim(), NumberStyles.None, Vietnamese, out quantity) || quantity < 1 || quantity > MaxQuantity)
        {
            error = $"Số lượng phải là số nguyên từ 1 đến {MaxQuantity:N0}.";
            return false;
        }

        var unitPrice = variant?.Price ?? product.BasePrice;
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(DiscountText)
            && (!decimal.TryParse(DiscountText.Trim(), NumberStyles.Number, Vietnamese, out discount) || discount < 0))
        {
            error = "Giảm giá phải là số không âm.";
            return false;
        }

        var gross = unitPrice * quantity;
        if (discount > gross)
        {
            error = $"Giảm giá không được lớn hơn thành tiền của dòng ({OrderPresentation.FormatMoney(gross)}).";
            return false;
        }

        line = new OrderDraftLine(
            product.Id,
            product.Name,
            variant?.Sku ?? product.Sku,
            variant?.Id,
            variant?.VariantName,
            product.IsSerialTracked,
            device?.Id,
            device?.Imei,
            device?.SerialNumber,
            unitPrice,
            quantity,
            discount);
        return true;
    }

    private static bool IsSameNonTrackedLine(OrderDraftLine existing, OrderDraftLine added) =>
        !existing.IsSerialTracked && existing.ProductId == added.ProductId && existing.VariantId == added.VariantId;

    private void ResetEditor()
    {
        Interlocked.Increment(ref _deviceVersion);
        SelectedProduct = null;
        Variants = [];
        SelectedVariant = null;
        _allDevices = [];
        _deviceTotal = 0;
        Devices = [];
        SelectedDevice = null;
        DeviceNotice = string.Empty;
        QuantityText = "1";
        DiscountText = string.Empty;
        LineError = string.Empty;
        IsLoadingDevices = false;
    }

    // ---------- submitting ----------

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private Task SaveDraft() => SubmitAsync(complete: false);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private Task PayAndComplete() => SubmitAsync(complete: true);

    [RelayCommand]
    private void BackToList() => _navigation.Navigate(AppRoute.Root(ShellDestination.Orders));

    private async Task SubmitAsync(bool complete)
    {
        if (!CanSubmit) return;
        if (Notes.Trim().Length > MaxNotesLength)
        {
            GeneralError = $"Ghi chú tối đa {MaxNotesLength} ký tự.";
            return;
        }

        GeneralError = string.Empty;
        PartialFailureMessage = string.Empty;
        Lines = Lines.Select(item => item with { Error = null }).ToArray();
        IsSubmitting = true;
        try
        {
            try
            {
                _createdOrder = await _orders.CreateAsync(BuildRequest(), ActivationToken);
            }
            catch (ApiException exception)
            {
                ShowCreateFailure(exception);
                return;
            }

            OnPropertyChanged(nameof(CreatedOrderCode));
            RefreshSubmitState();
            if (!complete)
            {
                FinishAndReturn(_createdOrder, completed: false);
                return;
            }

            try
            {
                var completedOrder = await _orders.UpdateStatusAsync(_createdOrder.Id, OrderStatus.Completed, ActivationToken);
                FinishAndReturn(completedOrder, completed: true);
            }
            catch (ApiException exception)
            {
                // The order exists as Pending. Submitting again would create a second order, so it stays locked.
                PartialFailureMessage =
                    $"Đơn {_createdOrder.OrderCode} đã được tạo ở trạng thái chờ xử lý nhưng chưa hoàn tất: {OrderApiErrors.Describe(exception, $"Đơn {_createdOrder.OrderCode}")}";
            }
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private CreateOrderRequest BuildRequest() => new()
    {
        PaymentMethod = SelectedPayment.Method.ToString(),
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        Items = Lines.Select(item => new CreateOrderItemRequest
        {
            ProductId = item.Line.ProductId,
            VariantId = item.Line.VariantId,
            SerialImeiId = item.Line.SerialImeiId,
            Quantity = item.Line.Quantity,
            DiscountAmount = item.Line.DiscountAmount
        }).ToList()
    };

    /// <summary>Server errors keyed <c>items[n].field</c> land on line n; everything else goes to the general area.</summary>
    private void ShowCreateFailure(ApiException exception)
    {
        if (exception.StatusCode != 400 || exception.FieldErrors.Count == 0)
        {
            GeneralError = OrderApiErrors.Describe(exception, "Đơn hàng");
            return;
        }

        var perLine = new Dictionary<int, List<string>>();
        var general = new List<string>();
        foreach (var (key, messages) in exception.FieldErrors)
        {
            var match = LineErrorKey.Match(key);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var index) && index >= 0 && index < Lines.Count)
            {
                if (!perLine.TryGetValue(index, out var list)) perLine[index] = list = [];
                list.AddRange(messages);
            }
            else
            {
                general.AddRange(messages);
            }
        }

        Lines = Lines
            .Select((item, index) => perLine.TryGetValue(index, out var list) ? item with { Error = string.Join(" ", list) } : item)
            .ToArray();
        GeneralError = general.Count > 0
            ? string.Join(" ", general)
            : perLine.Count > 0 ? "Một số dòng chưa hợp lệ. Hãy xem lỗi ở từng dòng." : OrderApiErrors.Describe(exception, "Đơn hàng");
    }

    private void FinishAndReturn(OrderDetail order, bool completed)
    {
        _navigation.Navigate(AppRoute.Root(ShellDestination.Orders));
        _ = InformCreatedAsync(order, completed);
    }

    private async Task InformCreatedAsync(OrderDetail order, bool completed)
    {
        try
        {
            var message = completed
                ? $"Đơn {order.OrderCode} đã hoàn tất. Tổng tiền {OrderPresentation.FormatMoney(order.TotalAmount)}."
                : $"Đơn {order.OrderCode} đã được lưu ở trạng thái chờ xử lý. Tổng tiền {OrderPresentation.FormatMoney(order.TotalAmount)}.";
            await _dialogs.InformAsync(new DialogRequest("Đã tạo đơn hàng", message, "Đóng"), CancellationToken.None);
        }
        catch (Exception)
        {
            // The order is saved and the list is already showing it; this notice is a courtesy, and it runs
            // detached from the command, so a failure here must not surface as an unobserved task error.
        }
    }
}
