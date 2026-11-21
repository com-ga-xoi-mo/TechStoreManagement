using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Orders;
using TechStore.Client.Models.Sales;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.DTOs;
using TechStore.Client.ViewModels.Customers;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Sales;

/// <summary>
/// Cash register: scan items into a cart, take cash and check out into the open shift. Amounts shown before payment
/// come from the same server pricing service used at checkout.
/// </summary>
public sealed partial class PosViewModel : PageViewModel
{
    public const int MaxLines = 100;
    public const int MaxQuantity = 10_000;

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Regex LineErrorKey = new(@"^items\[(\d+)\]\.?(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IPosRepository _pos;
    private readonly IOrderDeviceRepository _devices;
    private readonly IProductRepository _products;
    private readonly IPosDraftStore _drafts;
    private int _scanVersion;
    private Guid? _checkoutKey;

    [ObservableProperty] private PosShift? shift;
    [ObservableProperty] private string openingText = string.Empty;
    [ObservableProperty] private string countedCashText = string.Empty;
    [ObservableProperty] private string closingNote = string.Empty;
    [ObservableProperty] private bool isClosingShift;
    [ObservableProperty] private bool isShiftBusy;
    [ObservableProperty] private string shiftError = string.Empty;

    [ObservableProperty] private string scanText = string.Empty;
    [ObservableProperty] private string scanMessage = string.Empty;
    [ObservableProperty] private bool isScanning;

    [ObservableProperty] private IReadOnlyList<DraftLineItem> lines = [];
    [ObservableProperty] private string cashText = string.Empty;
    [ObservableProperty] private bool isPaying;
    [ObservableProperty] private string payError = string.Empty;
    [ObservableProperty] private bool awaitingRetry;
    [ObservableProperty] private OrderDetail? receipt;

    [ObservableProperty] private string discountText = string.Empty;
    [ObservableProperty] private PosQuoteDto? quote;
    [ObservableProperty] private string quoteError = string.Empty;
    private CancellationTokenSource? _quoteRequest;
    public CustomerPickerViewModel? Customer { get; }

    public PosViewModel(IPosRepository pos, IOrderDeviceRepository devices, IProductRepository products, IPosDraftStore drafts, CustomerPickerViewModel? customer = null)
    {
        Customer = customer;
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _pos = pos ?? throw new ArgumentNullException(nameof(pos));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _products = products ?? throw new ArgumentNullException(nameof(products));
    }

    public bool HasShift => Shift is { IsOpen: true };
    public bool ShowSale => HasShift && !IsClosingShift;
    public bool NoShift => !HasShift;
    public bool HasLines => Lines.Count > 0;
    public bool HasScanMessage => !string.IsNullOrWhiteSpace(ScanMessage);
    public bool HasPayError => !string.IsNullOrWhiteSpace(PayError);
    public bool HasShiftError => !string.IsNullOrWhiteSpace(ShiftError);
    public bool HasReceipt => Receipt is not null;
    public string ReceiptText => Receipt is null
        ? string.Empty
        : $"Đơn {Receipt.OrderCode} đã hoàn tất. Tổng {OrderPresentation.FormatMoney(Receipt.TotalAmount)} · Khách đưa {OrderPresentation.FormatMoney(Receipt.CashTendered ?? Receipt.TotalAmount)} · Tiền thừa {OrderPresentation.FormatMoney(Receipt.ChangeGiven ?? 0m)}";

    public decimal EstimatedTotal => Quote?.TotalAmount ?? 0m;
    public string EstimatedTotalText => Quote is null ? "—" : OrderPresentation.FormatMoney(Quote.TotalAmount);
    public string TaxText => Quote is null ? "—" : OrderPresentation.FormatMoney(Quote.TaxAmount);
    public bool HasQuoteError => QuoteError.Length > 0;

    /// <summary>The cash received, or null when the box is empty or not a valid amount.</summary>
    public decimal? CashReceived => TryParseMoney(CashText, out var cash) ? cash : null;

    public decimal Change => Quote is { } price && CashReceived is { } cash && cash >= price.TotalAmount ? cash - price.TotalAmount : 0m;
    public string ChangeText => Quote is null ? "—" : OrderPresentation.FormatMoney(Change);
    public bool CanPay => HasShift && HasLines && !IsPaying && !IsScanning && Quote is { } price && CashReceived is { } cash && cash >= price.TotalAmount;
    public bool CanEditCart => HasShift && !IsPaying && !AwaitingRetry;

    public string ShiftSummary => Shift is null
        ? string.Empty
        : $"Ca mở lúc {OrderPresentation.FormatDateTime(Shift.OpenedAt)} · Đầu ca {OrderPresentation.FormatMoney(Shift.OpeningBalance)} · Bán tiền mặt {OrderPresentation.FormatMoney(Shift.CashSalesTotal)}";

    public string ExpectedCashText => Shift is null ? string.Empty : OrderPresentation.FormatMoney(Shift.ExpectedCash);

    /// <summary>The counted cash minus the expected cash while closing, e.g. "Chênh lệch −50.000 ₫".</summary>
    public string DiscrepancyText
    {
        get
        {
            if (Shift is null || !TryParseMoney(CountedCashText, out var counted)) return string.Empty;
            var diff = counted - Shift.ExpectedCash;
            return diff == 0 ? "Khớp tiền dự kiến" : $"Chênh lệch {(diff > 0 ? "+" : "−")}{OrderPresentation.FormatMoney(Math.Abs(diff))}";
        }
    }

    public bool NeedsClosingNote => Shift is not null && TryParseMoney(CountedCashText, out var counted) && counted != Shift.ExpectedCash;
    public bool CanConfirmClose => IsClosingShift && !IsShiftBusy && Shift is not null
        && TryParseMoney(CountedCashText, out _) && (!NeedsClosingNote || !string.IsNullOrWhiteSpace(ClosingNote));

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        Shift = await _pos.GetCurrentShiftAsync(cancellationToken);
        if (Shift is { IsOpen: true } open && _drafts.Take(open.Id) is { } draft)
        {
            AwaitingRetry = draft.AwaitingRetry;
            if (Customer != null) Customer.SelectedCustomer = draft.Customer;
            DiscountText = draft.DiscountText;
            if (AwaitingRetry) Quote = draft.Quote;
            Lines = draft.Lines;
            CashText = draft.CashText;
            _checkoutKey = draft.CheckoutKey;
            CountedCashText = draft.CountedCashText;
            ClosingNote = draft.ClosingNote;
            IsClosingShift = draft.IsClosingShift;
        }

        return true;
    }

    /// <summary>Leaving the page (for example to Settings) must not throw away a sale or a half-filled close.</summary>
    protected override void OnDeactivated()
    {
        _quoteRequest?.Cancel();
        Customer?.Cancel();
        if (Shift is { IsOpen: true } open && (HasLines || IsClosingShift))
            _drafts.Save(new PosDraft(open.Id, Lines, CashText, _checkoutKey, AwaitingRetry || IsPaying, IsClosingShift, CountedCashText, ClosingNote,
                Customer?.SelectedCustomer, DiscountText, Quote));
    }

    // ---------- derived state ----------

    partial void OnShiftChanged(PosShift? value)
    {
        OnPropertyChanged(nameof(HasShift));
        OnPropertyChanged(nameof(NoShift));
        OnPropertyChanged(nameof(ShowSale));
        OnPropertyChanged(nameof(ShiftSummary));
        OnPropertyChanged(nameof(ExpectedCashText));
        OnPropertyChanged(nameof(DiscrepancyText));
        OnPropertyChanged(nameof(NeedsClosingNote));
        RefreshPayState();
    }

    partial void OnOpeningTextChanged(string value) => OpenShiftCommand.NotifyCanExecuteChanged();
    partial void OnIsShiftBusyChanged(bool value) => RefreshShiftState();
    partial void OnIsClosingShiftChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSale));
        RefreshShiftState();
    }

    partial void OnCountedCashTextChanged(string value) => RefreshShiftState();
    partial void OnClosingNoteChanged(string value) => RefreshShiftState();
    partial void OnShiftErrorChanged(string value) => OnPropertyChanged(nameof(HasShiftError));
    partial void OnScanMessageChanged(string value) => OnPropertyChanged(nameof(HasScanMessage));
    partial void OnPayErrorChanged(string value) => OnPropertyChanged(nameof(HasPayError));
    partial void OnReceiptChanged(OrderDetail? value)
    {
        OnPropertyChanged(nameof(HasReceipt));
        OnPropertyChanged(nameof(ReceiptText));
    }
    partial void OnCashTextChanged(string value) => RefreshPayState();
    partial void OnIsPayingChanged(bool value) => RefreshPayState();
    partial void OnAwaitingRetryChanged(bool value) => RefreshPayState();

    partial void OnIsScanningChanged(bool value) => RefreshPayState();
    partial void OnDiscountTextChanged(string value) => _ = RefreshQuoteAsync();
    partial void OnQuoteErrorChanged(string value) => OnPropertyChanged(nameof(HasQuoteError));
    partial void OnLinesChanged(IReadOnlyList<DraftLineItem>? oldValue, IReadOnlyList<DraftLineItem> newValue)
    {
        OnPropertyChanged(nameof(HasLines));
        if (oldValue == null || !oldValue.Select(i => i.Line).SequenceEqual(newValue.Select(i => i.Line)))
            _ = RefreshQuoteAsync();
        RefreshPayState();
    }

    partial void OnQuoteChanged(PosQuoteDto? value)
    {
        OnPropertyChanged(nameof(TaxText));
        OnPropertyChanged(nameof(EstimatedTotal));
        OnPropertyChanged(nameof(EstimatedTotalText));
        RefreshPayState();
    }

    [RelayCommand]
    private async Task RefreshQuoteAsync()
    {
        _quoteRequest?.Cancel();
        if (AwaitingRetry) return;
        Quote = null;
        QuoteError = string.Empty;
        if (!HasLines) return;
        if (!string.IsNullOrWhiteSpace(DiscountText)
            && (!TryParseMoney(DiscountText, out var discount) || decimal.Truncate(discount) != discount))
        {
            QuoteError = "Giảm giá đơn phải là số đồng nguyên không âm.";
            return;
        }

        using var request = CancellationTokenSource.CreateLinkedTokenSource(ActivationToken);
        _quoteRequest = request;
        try
        {
            var price = await _pos.QuoteAsync(BuildRequest(), request.Token);
            if (!request.IsCancellationRequested) Quote = price;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (ApiException error)
        {
            if (!request.IsCancellationRequested) QuoteError = OrderApiErrors.Describe(error, "Tính tiền");
        }
        finally
        {
            if (ReferenceEquals(_quoteRequest, request)) _quoteRequest = null;
        }
    }

    private PosCheckoutRequest BuildRequest() => new()
    {
        CustomerId = Customer?.SelectedCustomerId,
        OrderDiscountAmount = TryParseMoney(DiscountText, out var discount) ? discount : 0m,
        Items = Lines.Select(item => new CreateOrderItemRequest
        {
            ProductId = item.Line.ProductId,
            VariantId = item.Line.VariantId,
            SerialImeiId = item.Line.SerialImeiId,
            Quantity = item.Line.Quantity,
            DiscountAmount = item.Line.DiscountAmount
        }).ToList()
    };

    private void RefreshPayState()
    {
        OnPropertyChanged(nameof(CashReceived));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(CanPay));
        OnPropertyChanged(nameof(CanEditCart));
        PayCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
    }

    private void RefreshShiftState()
    {
        OnPropertyChanged(nameof(DiscrepancyText));
        OnPropertyChanged(nameof(NeedsClosingNote));
        OnPropertyChanged(nameof(CanConfirmClose));
        OpenShiftCommand.NotifyCanExecuteChanged();
        ConfirmCloseShiftCommand.NotifyCanExecuteChanged();
    }

    // ---------- shift ----------

    private bool CanOpenShift() => NoShift && !IsShiftBusy && TryParseMoney(OpeningText, out _);

    [RelayCommand(CanExecute = nameof(CanOpenShift))]
    private async Task OpenShift()
    {
        if (!TryParseMoney(OpeningText, out var opening)) return;

        ShiftError = string.Empty;
        IsShiftBusy = true;
        try
        {
            Shift = await _pos.OpenShiftAsync(opening, ActivationToken);
            OpeningText = string.Empty;
            Receipt = null;
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            ShiftError = OrderApiErrors.Describe(exception, "Ca làm việc");
        }
        finally
        {
            IsShiftBusy = false;
        }
    }

    [RelayCommand]
    private void BeginCloseShift()
    {
        if (!HasShift || HasLines) return;
        CountedCashText = string.Empty;
        ClosingNote = string.Empty;
        ShiftError = string.Empty;
        IsClosingShift = true;
    }

    [RelayCommand]
    private void CancelCloseShift() => IsClosingShift = false;

    [RelayCommand(CanExecute = nameof(CanConfirmClose))]
    private async Task ConfirmCloseShift()
    {
        if (Shift is not { } current || !TryParseMoney(CountedCashText, out var counted)) return;

        ShiftError = string.Empty;
        IsShiftBusy = true;
        try
        {
            await _pos.CloseShiftAsync(current.Id, counted, NeedsClosingNote ? ClosingNote : null, ActivationToken);
            IsClosingShift = false;
            Shift = null;
            Receipt = null;
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            ShiftError = OrderApiErrors.Describe(exception, "Ca làm việc");
        }
        finally
        {
            IsShiftBusy = false;
        }
    }

    // ---------- scanning ----------

    /// <summary>
    /// Adds what a scanner (or the keyboard) typed: an IMEI/serial adds that device, a SKU or barcode adds the product
    /// (a variant's own SKU or barcode picks the variant).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditCart))]
    private async Task Scan()
    {
        var code = ScanText?.Trim();
        if (string.IsNullOrEmpty(code) || !CanEditCart) return;

        var version = ++_scanVersion;
        ScanMessage = string.Empty;
        IsScanning = true;
        try
        {
            var message = await ResolveAsync(code, version);
            if (version != _scanVersion) return;

            ScanMessage = message ?? string.Empty;
            if (message is null) ScanText = string.Empty;
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException)
        {
            if (version == _scanVersion) ScanMessage = "Không thể tra cứu mã này. Hãy thử lại.";
        }
        finally
        {
            if (version == _scanVersion) IsScanning = false;
        }
    }

    /// <summary>Returns a message to show when nothing was added, or <c>null</c> after adding a line.</summary>
    private async Task<string?> ResolveAsync(string code, int version)
    {
        if (Lines.Count >= MaxLines) return $"Một đơn tối đa {MaxLines} dòng.";

        var scanned = await _devices.FindInStockByCodeAsync(code, ActivationToken);
        if (version != _scanVersion) return string.Empty;
        if (scanned is not null)
        {
            if (Lines.Any(item => item.Line.SerialImeiId == scanned.Device.Id)) return "Máy này đã có trong giỏ.";
            var product = await _products.GetByIdAsync(scanned.ProductId, ActivationToken);
            if (version != _scanVersion) return string.Empty;
            return AddDevice(product, scanned);
        }

        var page = await _products.SearchAsync(new ProductQuery { ExactCode = code, IsActive = true, Sort = "name:asc", PageSize = 10 }, ActivationToken);
        if (version != _scanVersion) return string.Empty;
        var hit = page.Items.Count == 1 ? page.Items[0] : null;
        if (hit is null)
            return page.Items.Count == 0 ? "Không tìm thấy máy hoặc sản phẩm với mã này." : "Có nhiều sản phẩm phù hợp. Hãy quét đúng SKU hoặc mã vạch.";

        var detail = await _products.GetByIdAsync(hit.Id, ActivationToken);
        if (version != _scanVersion) return string.Empty;
        return AddProduct(detail, code);
    }

    private string? AddDevice(ProductDetail? product, ScannedDevice scanned)
    {
        if (product is not { IsActive: true }) return "Sản phẩm của máy này đã ngừng kinh doanh.";

        var variant = scanned.VariantId.HasValue ? product.Variants.FirstOrDefault(v => v.Id == scanned.VariantId.Value) : null;
        if (scanned.VariantId.HasValue && variant is not { IsActive: true }) return "Biến thể của máy này đã ngừng kinh doanh.";

        return Append(new OrderDraftLine(
            product.Id, product.Name, variant?.Sku ?? product.Sku, variant?.Id, variant?.VariantName,
            IsSerialTracked: true, scanned.Device.Id, scanned.Device.Imei, scanned.Device.SerialNumber,
            variant?.Price ?? product.BasePrice, 1, 0m));
    }

    private string? AddProduct(ProductDetail? product, string code)
    {
        if (product is not { IsActive: true }) return "Sản phẩm đã ngừng kinh doanh.";
        bool Matches(string? value) => string.Equals(code, value, StringComparison.OrdinalIgnoreCase);
        var productMatch = Matches(product.Sku) || Matches(product.Barcode);
        if (!productMatch && !product.Variants.Any(v => v.IsActive && (Matches(v.Sku) || Matches(v.Barcode))))
            return "Không tìm thấy sản phẩm với mã này.";
        if (product.IsSerialTracked) return $"{product.Name} theo dõi IMEI: hãy quét IMEI hoặc serial của máy cần bán.";

        var active = product.Variants.Where(v => v.IsActive).ToArray();
        ProductVariant? variant = null;
        if (product.Variants.Count > 0)
        {
            variant = active.FirstOrDefault(v => string.Equals(v.Sku, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(v.Barcode, code, StringComparison.OrdinalIgnoreCase))
                ?? (productMatch && active.Length == 1 ? active[0] : null);
            if (variant is null) return $"{product.Name} có nhiều biến thể: hãy quét SKU hoặc mã vạch của biến thể.";
        }

        var existing = Lines.FirstOrDefault(item => !item.Line.IsSerialTracked
            && item.Line.ProductId == product.Id && item.Line.VariantId == variant?.Id);
        if (existing is not null) return ChangeQuantity(existing, existing.Line.Quantity + 1);

        return Append(new OrderDraftLine(
            product.Id, product.Name, variant?.Sku ?? product.Sku, variant?.Id, variant?.VariantName,
            IsSerialTracked: false, null, null, null, variant?.Price ?? product.BasePrice, 1, 0m));
    }

    private string? Append(OrderDraftLine line)
    {
        Lines = [.. Lines, new DraftLineItem(line)];
        _checkoutKey = null;
        return null;
    }

    // ---------- cart ----------

    [RelayCommand]
    private void IncreaseQuantity(object? parameter)
    {
        if (parameter is DraftLineItem item && CanEditCart && !item.Line.IsSerialTracked)
            ScanMessage = ChangeQuantity(item, item.Line.Quantity + 1) ?? string.Empty;
    }

    [RelayCommand]
    private void DecreaseQuantity(object? parameter)
    {
        if (parameter is DraftLineItem item && CanEditCart && !item.Line.IsSerialTracked && item.Line.Quantity > 1)
            ScanMessage = ChangeQuantity(item, item.Line.Quantity - 1) ?? string.Empty;
    }

    [RelayCommand]
    private void RemoveLine(object? parameter)
    {
        if (parameter is not DraftLineItem item || !CanEditCart || !Lines.Any(l => ReferenceEquals(l, item))) return;
        Lines = Lines.Where(l => !ReferenceEquals(l, item)).ToArray();
        _checkoutKey = null;
        PayError = string.Empty;
    }

    private string? ChangeQuantity(DraftLineItem item, int quantity)
    {
        if (quantity > MaxQuantity) return $"Số lượng của một sản phẩm tối đa {MaxQuantity:N0}.";

        var changed = item.Line with { Quantity = quantity, DiscountAmount = 0m };
        Lines = Lines.Select(l => ReferenceEquals(l, item) ? new DraftLineItem(changed) : l).ToArray();
        _checkoutKey = null;
        return null;
    }

    [RelayCommand]
    private void ClearCart()
    {
        if (!CanEditCart) return;
        Lines = [];
        Customer?.Reset();
        DiscountText = string.Empty;
        CashText = string.Empty;
        PayError = string.Empty;
        _checkoutKey = null;
    }

    // ---------- paying ----------

    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task Pay()
    {
        if (!CanPay || Shift is not { } current || CashReceived is not { } cash) return;

        // One key per sale. It survives a retry after an unknown outcome, so the server can answer with the stored order.
        _checkoutKey ??= Guid.NewGuid();
        PayError = string.Empty;
        Lines = Lines.Select(item => item with { Error = null }).ToArray();
        IsPaying = true;
        try
        {
            var request = BuildRequest();
            request.PosSessionId = current.Id;
            request.CheckoutKey = _checkoutKey;
            request.CashTendered = cash;
            request.ExpectedTotal = Quote!.TotalAmount;
            var order = await _pos.CheckoutAsync(request, ActivationToken);

            Receipt = order;
            AwaitingRetry = false;
            Lines = [];
            Customer?.Reset();
            DiscountText = string.Empty;
            CashText = string.Empty;
            _checkoutKey = null;
            await RefreshShiftAfterSaleAsync();
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            ShowPayFailure(exception);
            if (exception.StatusCode == 409) await RefreshQuoteAsync();
        }
        finally
        {
            IsPaying = false;
        }
    }

    /// <summary>Shows the new cash total. The sale is already stored, so a failure here must not look like a failed sale.</summary>
    private async Task RefreshShiftAfterSaleAsync()
    {
        try
        {
            Shift = await _pos.GetCurrentShiftAsync(ActivationToken) ?? Shift;
        }
        catch (ApiException)
        {
        }
    }

    private void ShowPayFailure(ApiException exception)
    {
        // Timeouts, lost connections and server errors may have stored the sale: keep the cart and the key, so Pay
        // again asks the server instead of risking a second sale.
        var unknown = exception.Kind != ApiErrorKind.Http || exception.StatusCode >= 500;
        AwaitingRetry = unknown;
        if (unknown)
        {
            PayError = "Chưa xác định được kết quả thanh toán. Hãy bấm Thanh toán lại: hệ thống sẽ không ghi nhận bán hai lần.";
            return;
        }

        _checkoutKey = null;
        if (exception.StatusCode == 400 && exception.FieldErrors.Count > 0)
        {
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
            PayError = general.Count > 0 ? string.Join(" ", general) : "Một số dòng chưa hợp lệ. Hãy xem lỗi ở từng dòng.";
            return;
        }

        PayError = OrderApiErrors.Describe(exception, "Đơn hàng");
    }

    [RelayCommand]
    private void DismissReceipt() => Receipt = null;

    private static bool TryParseMoney(string? text, out decimal amount)
    {
        amount = 0;
        return !string.IsNullOrWhiteSpace(text)
            && decimal.TryParse(text.Trim(), NumberStyles.Number, Vietnamese, out amount)
            && amount >= 0
            && decimal.Round(amount, 2) == amount;
    }
}
