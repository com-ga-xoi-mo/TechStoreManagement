using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Sales;
using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Enums;

namespace TechStore.Client.ViewModels.Sales;

/// <summary>
/// For Admin and Manager: VietQR requests still waiting (confirm one by hand from the bank statement) and bank money that
/// did not complete a sale (mark it handled with a note). Pick a row, then fill the form below the lists.
/// </summary>
public sealed partial class VietQrReconcileViewModel : PageViewModel
{
    public const int MaxNoteLength = 500;

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly IVietQrRepository _vietQr;
    private readonly INavigationService _navigation;

    [ObservableProperty] private IReadOnlyList<VietQrPayment> pending = [];
    [ObservableProperty] private IReadOnlyList<VietQrBankEvent> events = [];
    [ObservableProperty] private VietQrPayment? selectedPayment;
    [ObservableProperty] private string referenceText = string.Empty;
    [ObservableProperty] private string amountText = string.Empty;
    [ObservableProperty] private VietQrBankEvent? selectedEvent;
    [ObservableProperty] private string noteText = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string actionError = string.Empty;
    [ObservableProperty] private string actionMessage = string.Empty;

    public VietQrReconcileViewModel(IVietQrRepository vietQr, INavigationService navigation)
    {
        _vietQr = vietQr ?? throw new ArgumentNullException(nameof(vietQr));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public bool HasPending => Pending.Count > 0;
    public bool HasNoPending => Pending.Count == 0;
    public bool HasEvents => Events.Count > 0;
    public bool HasNoEvents => Events.Count == 0;
    public bool HasSelectedPayment => SelectedPayment is not null;
    public bool HasSelectedEvent => SelectedEvent is { NeedsReconciliation: true };
    public bool HasActionError => ActionError.Length > 0;
    public bool HasActionMessage => ActionMessage.Length > 0;
    public string SelectedPaymentText => SelectedPayment is null
        ? string.Empty
        : $"Đơn {SelectedPayment.OrderCode} · mã {SelectedPayment.TransactionCode} · cần nhận {FormatMoney(SelectedPayment.Amount)}";
    public string SelectedEventText => SelectedEvent is null
        ? string.Empty
        : $"{SelectedEvent.Provider} {SelectedEvent.ProviderTransactionId} · {FormatMoney(SelectedEvent.Amount)} · {VietQrPresentation.ReasonText(SelectedEvent.Reason)}";

    public bool CanConfirm => !IsBusy && SelectedPayment is not null
        && !string.IsNullOrWhiteSpace(ReferenceText) && TryParseAmount(AmountText, out _);
    public bool CanResolve => !IsBusy && SelectedEvent is { NeedsReconciliation: true }
        && !string.IsNullOrWhiteSpace(NoteText) && NoteText.Trim().Length <= MaxNoteLength;

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        var requests = _vietQr.ListAsync(VietQrStatus.Pending, null, cancellationToken);
        var held = _vietQr.ListEventsAsync(VietQrEventStatus.NeedsReconciliation, cancellationToken);
        await Task.WhenAll(requests, held);
        Pending = requests.Result;
        Events = held.Result;
        // Keep the selection only while the row is still open; a confirmed or resolved row leaves the list.
        if (SelectedPayment is { } payment && Pending.All(p => p.Id != payment.Id)) ClearPayment();
        if (SelectedEvent is { } selected && Events.All(e => e.Id != selected.Id)) ClearEvent();
        return true;
    }

    partial void OnPendingChanged(IReadOnlyList<VietQrPayment> value)
    {
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(HasNoPending));
    }

    partial void OnEventsChanged(IReadOnlyList<VietQrBankEvent> value)
    {
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(HasNoEvents));
    }

    partial void OnSelectedPaymentChanged(VietQrPayment? value)
    {
        OnPropertyChanged(nameof(HasSelectedPayment));
        OnPropertyChanged(nameof(SelectedPaymentText));
        RefreshConfirm();
    }

    partial void OnSelectedEventChanged(VietQrBankEvent? value)
    {
        OnPropertyChanged(nameof(HasSelectedEvent));
        OnPropertyChanged(nameof(SelectedEventText));
        RefreshResolve();
    }

    partial void OnReferenceTextChanged(string value) => RefreshConfirm();
    partial void OnAmountTextChanged(string value) => RefreshConfirm();
    partial void OnNoteTextChanged(string value) => RefreshResolve();
    partial void OnIsBusyChanged(bool value)
    {
        RefreshConfirm();
        RefreshResolve();
    }

    partial void OnActionErrorChanged(string value) => OnPropertyChanged(nameof(HasActionError));
    partial void OnActionMessageChanged(string value) => OnPropertyChanged(nameof(HasActionMessage));

    private void RefreshConfirm()
    {
        OnPropertyChanged(nameof(CanConfirm));
        ConfirmPaymentCommand.NotifyCanExecuteChanged();
    }

    private void RefreshResolve()
    {
        OnPropertyChanged(nameof(CanResolve));
        ResolveEventCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Back() => _navigation.Navigate(ShellDestination.Orders);

    [RelayCommand]
    private Task Refresh()
    {
        ActionError = string.Empty;
        ActionMessage = string.Empty;
        return RetryContentAsync();
    }

    // The row command parameter can briefly be the page's DataContext while a row is removed, so it is checked, not cast.
    [RelayCommand]
    private void SelectPayment(object? parameter)
    {
        if (parameter is not VietQrPayment payment) return;
        SelectedPayment = payment;
        ReferenceText = string.Empty;
        AmountText = decimal.Truncate(payment.Amount).ToString("0", Vietnamese);
        ActionError = string.Empty;
        ActionMessage = string.Empty;
    }

    [RelayCommand]
    private void SelectEvent(object? parameter)
    {
        if (parameter is not VietQrBankEvent bankEvent) return;
        SelectedEvent = bankEvent;
        NoteText = string.Empty;
        ActionError = string.Empty;
        ActionMessage = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmPayment()
    {
        if (!CanConfirm || SelectedPayment is not { } payment || !TryParseAmount(AmountText, out var amount)) return;

        ActionError = string.Empty;
        ActionMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _vietQr.ConfirmAsync(payment.Id, ReferenceText.Trim(), amount, ActivationToken);
            ClearPayment();
            ActionMessage = $"Đã xác nhận thanh toán đơn {payment.OrderCode}.";
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            ActionError = OrderApiErrors.Describe(exception, "Xác nhận VietQR");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanResolve))]
    private async Task ResolveEvent()
    {
        if (!CanResolve || SelectedEvent is not { } bankEvent) return;

        ActionError = string.Empty;
        ActionMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _vietQr.ResolveEventAsync(bankEvent.Id, NoteText.Trim(), ActivationToken);
            ClearEvent();
            ActionMessage = $"Đã ghi nhận xử lý khoản {bankEvent.ProviderTransactionId}.";
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            ActionError = OrderApiErrors.Describe(exception, "Đối soát VietQR");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reloads both lists after a change. A failed reload must not hide that the change itself succeeded.</summary>
    private async Task ReloadAsync()
    {
        try
        {
            await LoadContentAsync(ActivationToken);
        }
        catch (ApiException)
        {
            ActionError = "Đã lưu, nhưng chưa tải lại được danh sách. Hãy bấm Làm mới.";
        }
    }

    private void ClearPayment()
    {
        SelectedPayment = null;
        ReferenceText = string.Empty;
        AmountText = string.Empty;
    }

    private void ClearEvent()
    {
        SelectedEvent = null;
        NoteText = string.Empty;
    }

    private static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0;
        return !string.IsNullOrWhiteSpace(text)
            && decimal.TryParse(text.Trim(), NumberStyles.Number, Vietnamese, out amount)
            && amount > 0
            && decimal.Round(amount, 2) == amount;
    }

    private static string FormatMoney(decimal amount) => TechStore.Client.Controls.Presentation.OrderPresentation.FormatMoney(amount);
}
