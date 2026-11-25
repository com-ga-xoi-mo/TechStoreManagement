using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Warranty;
using TechStore.Client.Repositories.Warranty;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Warranty;

public sealed partial class WarrantyIntakeDialogViewModel : PageViewModel
{
    private readonly IWarrantyRepository repository;
    public CustomerPickerViewModel CustomerPicker { get; }

    public WarrantyIntakeDialogViewModel(IWarrantyRepository repository, CustomerPickerViewModel customerPicker)
    {
        this.repository = repository;
        CustomerPicker = customerPicker;
        CustomerPicker.AllowCreate = false;
    }
    public Guid CaseId { get; } = Guid.NewGuid();
    public bool Succeeded { get; private set; }
    public event Action? CaseChanged;

    [ObservableProperty] private string code = string.Empty;
    [ObservableProperty] private string orderCode = string.Empty;
    [ObservableProperty] private string issue = string.Empty;
    [ObservableProperty] private string contactName = string.Empty;
    [ObservableProperty] private string contactPhone = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowLookup))] private bool showIntake;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string lookupSummary = string.Empty;
    [ObservableProperty] private bool showCustomerPicker;

    private string? _verifiedCode;
    private string? _verifiedOrderCode;
    private Guid? _verifiedCustomer;

    public bool ShowLookup => !ShowIntake;

    [RelayCommand]
    private Task LookupAsync() => RunTrackedRequestAsync(async token =>
    {
        if (IsBusy) return;
        ErrorMessage = "";
        LookupSummary = "";
        if (string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Nhập IMEI hoặc số sê-ri.";
            return;
        }
        var inputCode = Code.Trim();
        var order = OrderCode.Trim();
        var customer = CustomerPicker.SelectedCustomerId;
        IsBusy = true;
        try
        {
            var result = await repository.LookupAsync(inputCode, order, customer, token);
            token.ThrowIfCancellationRequested();
            LookupSummary = $"{result.Product} · {result.Device}\nHạn bảo hành: {WarrantyLabels.Date(result.WarrantyEndAt)}";
            if (!result.Eligible)
            {
                ErrorMessage = string.Join("\n", result.Reasons.Select(WarrantyLabels.Reason));
                return;
            }
            _verifiedCode = inputCode;
            _verifiedOrderCode = order;
            _verifiedCustomer = customer;
            ContactName = result.CustomerName ?? "";
            ContactPhone = result.CustomerPhone ?? "";
            ShowIntake = true;
        }
        catch (WarrantyRequestException error) when (!token.IsCancellationRequested) { ErrorMessage = error.Message; }
        finally { IsBusy = false; }
    });

    [RelayCommand]
    private void ToggleCustomerPicker() => ShowCustomerPicker = !ShowCustomerPicker;

    [RelayCommand]
    private void Back()
    {
        ShowIntake = false;
        LookupSummary = "";
        ErrorMessage = "";
    }

    [RelayCommand]
    private Task SubmitAsync() => RunTrackedRequestAsync(async token =>
    {
        if (IsBusy || !ShowIntake) return;
        if (string.IsNullOrWhiteSpace(Issue) || string.IsNullOrWhiteSpace(ContactName) || string.IsNullOrWhiteSpace(ContactPhone))
        {
            ErrorMessage = "Nhập lỗi khách báo, tên và số điện thoại liên hệ.";
            return;
        }
        ErrorMessage = "";
        IsBusy = true;
        try
        {
            await repository.ReceiveAsync(CaseId, new ReceiveWarrantyRequest(_verifiedCode, _verifiedOrderCode,
                _verifiedCustomer, Issue, ContactName, ContactPhone), token);
            token.ThrowIfCancellationRequested();
            Succeeded = true;
            CaseChanged?.Invoke();
        }
        catch (WarrantyRequestException error) when (!token.IsCancellationRequested) { ErrorMessage = error.Message; }
        finally { IsBusy = false; }
    });

    protected override void OnDeactivated() => CustomerPicker.Cancel();
}
