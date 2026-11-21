using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;

using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class DeliveryLineDraft(PurchaseReceiptLine line) : ObservableObject
{
    public PurchaseReceiptLine Line => line;
    public string Label => $"{line.DisplayName} ({line.DisplaySku}) · Còn {line.OrderedQuantity - line.Quantity}";
    [ObservableProperty] private string quantityText = string.Empty;
    [ObservableProperty] private string codes = string.Empty;
}

public sealed partial class PurchaseReceiptDetailDialogViewModel(IPurchaseReceiptRepository receiptRepository, bool canWrite = false) : ObservableObject
{
    [ObservableProperty] private PurchaseReceiptDetail? _receipt;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private IReadOnlyList<DeliveryLineDraft> deliveryLines = [];
    [ObservableProperty] private bool confirmCancel;
    [ObservableProperty] private string deliveryDocument = string.Empty;
    private ReceivePurchaseDeliveryRequest? _pendingDelivery;
    public bool Changed { get; private set; }
    public bool IsOpenForDelivery => canWrite && Receipt?.Status is PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived;
    public bool CanEdit => IsOpenForDelivery && !IsLoading && _pendingDelivery == null;
    public bool CanReceive => IsOpenForDelivery && !IsLoading;
    public bool CanCancel => CanEdit && ConfirmCancel;
    public string StatusLabel => Receipt is null ? "" : ReceiptStatusLabels.Format(Receipt.Status);

    partial void OnReceiptChanged(PurchaseReceiptDetail? value)
    {
        DeliveryLines = value?.Lines.Where(l => l.OrderedQuantity > l.Quantity).Select(l => new DeliveryLineDraft(l)).ToList() ?? [];
        ConfirmCancel = false;
        DeliveryDocument = string.Empty;
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsOpenForDelivery));
        NotifyActions();
    }

    partial void OnConfirmCancelChanged(bool value) => OnPropertyChanged(nameof(CanCancel));
    partial void OnIsLoadingChanged(bool value) => NotifyActions();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    private void NotifyActions()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanReceive));
        OnPropertyChanged(nameof(CanCancel));
    }

    [RelayCommand]
    private async Task ReceiveAsync()
    {
        if (!CanReceive || Receipt == null) return;
        if (_pendingDelivery == null)
        {
            var delivery = new ReceivePurchaseDeliveryRequest { ClientRequestId = Guid.NewGuid(), SupplierDocumentNumber = DeliveryDocument };
            foreach (var draft in DeliveryLines)
            {
                if (string.IsNullOrWhiteSpace(draft.QuantityText) && string.IsNullOrWhiteSpace(draft.Codes)) continue;
                var line = draft.Line;
                var devices = ReceiptLineDraft.ParseDevices(draft.Codes);
                if (!int.TryParse(draft.QuantityText, out var quantity) || quantity < 1 || quantity > line.OrderedQuantity - line.Quantity
                    || devices.Count != (line.IsSerialTracked ? quantity : 0))
                {
                    ErrorMessage = $"Kiểm tra số lượng thực nhận và mã máy của {line.DisplayName}.";
                    return;
                }
                delivery.Lines.Add(new() { LineId = line.Id, ExpectedReceivedQuantity = line.Quantity, Quantity = quantity, Devices = devices });
            }
            if (delivery.Lines.Count == 0)
            {
                ErrorMessage = "Nhập số lượng cho ít nhất một dòng.";
                return;
            }
            _pendingDelivery = delivery;
        }
        await SubmitAsync(cancel: false);
    }

    [RelayCommand]
    private async Task CancelRemainderAsync()
    {
        if (CanCancel) await SubmitAsync(cancel: true);
    }

    private async Task SubmitAsync(bool cancel)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = cancel ? await receiptRepository.CancelRemainderAsync(Receipt!.Id)
                : await receiptRepository.ReceiveAsync(Receipt!.Id, _pendingDelivery!);
            if (result.Failure != ReceiptFailureKind.Network) _pendingDelivery = null;
            if (result.Succeeded && result.Data != null)
            {
                Receipt = result.Data;
                Changed = true;
            }
            else ErrorMessage = result.Failure == ReceiptFailureKind.Network
                ? "Chưa rõ kết quả. Hãy thử lại cùng thao tác hoặc tải lại phiếu."
                : result.Message ?? "Không thể cập nhật phiếu. Hãy tải lại phiếu.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (!IsLoading && Receipt is { } receipt) await LoadAsync(receipt.Id);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string Title => Receipt is null ? "Chi tiết phiếu nhập" : $"Chi tiết phiếu nhập {Receipt.PoNumber}";

    public event EventHandler? RequestClose;

    [RelayCommand]
    public async Task LoadAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            var result = await receiptRepository.GetByIdAsync(receiptId, cancellationToken);
            if (result.Succeeded && result.Data != null)
            {
                _pendingDelivery = null;
                Receipt = result.Data;
            }
            else
            {
                ErrorMessage = result.Failure switch
                {
                    ReceiptFailureKind.NotFound => "Phiếu nhập không tồn tại.",
                    ReceiptFailureKind.Forbidden => "Bạn không có quyền xem phiếu nhập.",
                    ReceiptFailureKind.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                    _ => "Không thể tải chi tiết phiếu nhập. Vui lòng thử lại."
                };
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể tải chi tiết phiếu nhập. Vui lòng thử lại.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(Title));
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
