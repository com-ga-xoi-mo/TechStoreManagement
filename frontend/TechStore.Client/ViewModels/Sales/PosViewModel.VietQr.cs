using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Orders;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Sales;

/// <summary>
/// Paying by VietQR: create the request (the server holds the goods for 15 minutes), show the code with a
/// countdown, and ask the server for the result every few seconds. The cart stays locked while the request is open.
/// </summary>
public sealed partial class PosViewModel
{
    public const int QrPollSeconds = 3;

    private CancellationTokenSource? _poll;
    private int _ticks;

    [ObservableProperty] private bool payByQr;
    [ObservableProperty] private VietQrPayment? qr;
    [ObservableProperty] private bool isQrBusy;
    [ObservableProperty] private string qrError = string.Empty;
    [ObservableProperty] private string qrNotice = string.Empty;
    [ObservableProperty] private string qrCountdownText = string.Empty;
    [ObservableProperty] private VietQrPayment? qrReceipt;

    public bool IsQrActive => Qr is { IsPending: true };
    public bool PayByCash => !PayByQr;
    public string? QrModules => Qr?.QrModules;
    public bool HasShiftQr => ShiftQrText.Length > 0;
    public bool HasQrError => QrError.Length > 0;
    public bool HasQrNotice => QrNotice.Length > 0;
    public bool CanChoosePayment => !IsQrActive && !IsQrBusy && !IsPaying;
    public bool CanCreateQr => PayByQr && HasShift && HasLines && !IsScanning && !IsPaying && !IsQrBusy && !IsQrActive
        && Quote is not null && _vietQr is not null;
    public string QrAmountText => Qr is null ? string.Empty : OrderPresentation.FormatMoney(Qr.Amount);
    public string QrBankText => Qr is null
        ? string.Empty
        : $"{Qr.BankAccountName} · {Qr.BankAccountNumber} · BIN {Qr.BankBin}".Trim(' ', '·');
    public string QrReceiptText => QrReceipt is null
        ? string.Empty
        : $"Đơn {QrReceipt.OrderCode} đã thanh toán qua VietQR. Tổng {OrderPresentation.FormatMoney(QrReceipt.Amount)} · Mã chuyển khoản {QrReceipt.TransactionCode}";
    public string ShiftQrText => Shift is { VietQrSalesTotal: > 0 } s ? $"Bán VietQR {OrderPresentation.FormatMoney(s.VietQrSalesTotal)} (không tính vào két)" : string.Empty;

    partial void OnPayByQrChanged(bool value)
    {
        OnPropertyChanged(nameof(PayByCash));
        RefreshQrState();
    }
    partial void OnIsQrBusyChanged(bool value) => RefreshQrState();
    partial void OnQrErrorChanged(string value) => OnPropertyChanged(nameof(HasQrError));

    /// <summary>Closing the shift is refused while a request is open; the cart is locked so the button must say why.</summary>
    private bool ExplainOpenQr()
    {
        if (Qr is not { IsPending: true } open) return false;
        QrError = $"Chưa đóng ca được: đơn {open.OrderCode} đang chờ thanh toán VietQR. Hãy hủy mã QR hoặc chờ xác nhận.";
        return true;
    }
    partial void OnQrNoticeChanged(string value) => OnPropertyChanged(nameof(HasQrNotice));
    partial void OnQrReceiptChanged(VietQrPayment? value)
    {
        OnPropertyChanged(nameof(HasReceipt));
        OnPropertyChanged(nameof(ReceiptText));
        OnPropertyChanged(nameof(QrReceiptText));
    }

    partial void OnQrChanged(VietQrPayment? value)
    {
        OnPropertyChanged(nameof(QrAmountText));
        OnPropertyChanged(nameof(QrBankText));
        OnPropertyChanged(nameof(QrModules));
        UpdateCountdown();
        RefreshQrState();
    }

    private void RefreshQrState()
    {
        OnPropertyChanged(nameof(IsQrActive));
        OnPropertyChanged(nameof(CanChoosePayment));
        OnPropertyChanged(nameof(CanCreateQr));
        OnPropertyChanged(nameof(CanPay));
        OnPropertyChanged(nameof(CanEditCart));
        CreateQrCommand.NotifyCanExecuteChanged();
        CancelQrCommand.NotifyCanExecuteChanged();
        PayCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanChoosePayment))]
    private void ChoosePayment(object? parameter)
    {
        if (!CanChoosePayment) return;
        PayByQr = parameter is "VietQR" or true;
        QrError = string.Empty;
        QrNotice = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanCreateQr))]
    private async Task CreateQr()
    {
        if (!CanCreateQr || _vietQr is null || Shift is not { } current) return;

        // One key per request. It survives an unknown outcome, so pressing again returns the stored request.
        _checkoutKey ??= Guid.NewGuid();
        QrError = string.Empty;
        QrNotice = string.Empty;
        IsQrBusy = true;
        try
        {
            var created = await _vietQr.CreateAsync(BuildQrRequest(current.Id), ActivationToken);
            Qr = created;
            StartPolling();
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            var unknown = exception.Kind != ApiErrorKind.Http || exception.StatusCode >= 500 && exception.StatusCode != 503;
            if (!unknown) _checkoutKey = null;
            QrError = unknown
                ? "Chưa xác định được kết quả tạo mã. Hãy bấm Tạo mã QR lại: hệ thống sẽ không tạo hai đơn."
                : exception.StatusCode == 503
                    ? "Cửa hàng chưa cấu hình tài khoản nhận tiền VietQR. Hãy báo quản lý."
                    : OrderApiErrors.Describe(exception, "Mã VietQR");
            if (exception.StatusCode == 409) await RefreshQuoteAsync();
        }
        finally
        {
            IsQrBusy = false;
        }
    }

    private CreateVietQrRequest BuildQrRequest(Guid shiftId)
    {
        var basis = BuildRequest();
        return new CreateVietQrRequest
        {
            PosSessionId = shiftId,
            CheckoutKey = _checkoutKey,
            CustomerId = basis.CustomerId,
            OrderDiscountAmount = basis.OrderDiscountAmount,
            ExpectedTotal = Quote?.TotalAmount,
            Items = basis.Items
        };
    }

    private bool CanCancelQr() => IsQrActive && !IsQrBusy;

    [RelayCommand(CanExecute = nameof(CanCancelQr))]
    private async Task CancelQr()
    {
        if (_vietQr is null || Qr is not { IsPending: true } current) return;

        QrError = string.Empty;
        IsQrBusy = true;
        try
        {
            await _vietQr.CancelAsync(current.Id, ActivationToken);
            EndQr("Đã hủy mã QR. Giỏ hàng được mở lại.");
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            // 409: the money may have arrived a moment ago. Ask the server instead of guessing.
            QrError = OrderApiErrors.Describe(exception, "Mã VietQR");
            await PollAsync(ActivationToken);
        }
        finally
        {
            IsQrBusy = false;
        }
    }

    // ---------- countdown and polling ----------

    /// <summary>One second of the loop: refresh the countdown and, every few seconds or at the deadline, ask the server.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (Qr is not { IsPending: true } current) return;

        UpdateCountdown();
        var overdue = _time.GetUtcNow().UtcDateTime >= current.ExpiresAt;
        if (++_ticks % QrPollSeconds == 0 || overdue)
            await PollAsync(cancellationToken);
    }

    /// <summary>Asks the server for the request's state. A network failure keeps polling; a final state ends the request.</summary>
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        if (_vietQr is null || Qr is not { } current) return;

        try
        {
            var latest = await _vietQr.GetAsync(current.Id, cancellationToken);
            if (Qr?.Id != current.Id) return;

            switch (latest.Status)
            {
                case VietQrStatus.Pending:
                    Qr = latest with { QrImagePng = latest.QrImagePng ?? current.QrImagePng, QrModules = latest.QrModules ?? current.QrModules };
                    QrError = string.Empty;
                    break;
                case VietQrStatus.Confirmed:
                    await FinishQrSaleAsync(latest);
                    break;
                case VietQrStatus.Expired:
                    EndQr("Mã QR đã hết hạn. Hãy tạo mã mới.");
                    break;
                default:
                    EndQr("Mã QR đã bị hủy.");
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ApiException)
        {
            QrError = "Không kiểm tra được trạng thái thanh toán. Đang thử lại.";
        }
    }

    private async Task FinishQrSaleAsync(VietQrPayment paid)
    {
        StopPolling();
        QrReceipt = paid with { QrImagePng = null, QrModules = null };
        Receipt = null;
        Qr = null;
        QrError = string.Empty;
        AwaitingRetry = false;
        Lines = [];
        Customer?.Reset();
        DiscountText = string.Empty;
        CashText = string.Empty;
        _checkoutKey = null;
        PayByQr = false;
        await RefreshShiftAfterSaleAsync();
    }

    /// <summary>The request ended without a sale: unlock the cart and start a new request with a new key.</summary>
    private void EndQr(string notice)
    {
        StopPolling();
        Qr = null;
        QrError = string.Empty;
        QrNotice = notice;
        _checkoutKey = null;
        _ = RefreshQuoteAsync();
    }

    private void UpdateCountdown() =>
        QrCountdownText = Qr is { IsPending: true } current
            ? VietQrPresentation.Countdown(current.ExpiresAt - _time.GetUtcNow().UtcDateTime)
            : string.Empty;

    private void StartPolling()
    {
        StopPolling();
        _ticks = 0;
        var source = CancellationTokenSource.CreateLinkedTokenSource(ActivationToken);
        _poll = source;
        _ = RunPollingAsync(source.Token);
    }

    private void StopPolling()
    {
        _poll?.Cancel();
        _poll?.Dispose();
        _poll = null;
    }

    private async Task RunPollingAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && Qr is { IsPending: true })
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _time, token);
                await TickAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Coming back to the page: the saved draft names the request, otherwise the server's open request of this shift is
    /// used, so a request created before the app was closed is not lost.
    /// </summary>
    private async Task ResumeQrAsync(Guid? draftQrId, CancellationToken cancellationToken)
    {
        if (_vietQr is null || Shift is not { IsOpen: true } open) return;

        try
        {
            var found = draftQrId is { } id
                ? await _vietQr.GetAsync(id, cancellationToken)
                : (await _vietQr.ListAsync(VietQrStatus.Pending, open.Id, cancellationToken)).FirstOrDefault();
            if (found is null) return;

            switch (found.Status)
            {
                case VietQrStatus.Pending:
                    // The list carries no image; the single read does.
                    Qr = found.QrModules is null ? await _vietQr.GetAsync(found.Id, cancellationToken) : found;
                    PayByQr = true;
                    StartPolling();
                    break;
                case VietQrStatus.Confirmed when draftQrId is not null:
                    await FinishQrSaleAsync(found);
                    break;
                case VietQrStatus.Expired or VietQrStatus.Failed when draftQrId is not null:
                    EndQr(found.Status == VietQrStatus.Expired ? "Mã QR đã hết hạn. Hãy tạo mã mới." : "Mã QR đã bị hủy.");
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ApiException)
        {
            QrError = "Không tải được mã QR đang chờ. Hãy mở lại trang.";
        }
    }
}
