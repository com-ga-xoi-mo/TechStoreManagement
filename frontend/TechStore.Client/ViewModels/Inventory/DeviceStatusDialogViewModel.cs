using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Repositories.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public enum DeviceStatusMode
{
    ReportDefective,
    Restore
}

public sealed partial class DeviceStatusDialogViewModel : ObservableObject
{
    private readonly ISerialDeviceRepository _repository;

    public IReadOnlyList<string> PresetReasons { get; } = ["Hư hỏng", "Mất"];

    [ObservableProperty] private SerialImeiDto? _device;
    [ObservableProperty] private DeviceStatusMode _mode = DeviceStatusMode.ReportDefective;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public bool IsReportDefective => Mode == DeviceStatusMode.ReportDefective;
    public bool IsRestore => Mode == DeviceStatusMode.Restore;

    public string Title => IsReportDefective ? "Báo lỗi máy" : "Sửa xong, đưa về kho";
    public string PrimaryButtonText => IsReportDefective ? "Báo lỗi" : "Đưa về kho";

    public string DeviceIdentifier => Device is not null
        ? (!string.IsNullOrWhiteSpace(Device.Imei) ? $"IMEI: {Device.Imei}" : $"Serial: {Device.SerialNumber}")
        : string.Empty;

    public string DeviceDescription => Device is not null
        ? (!string.IsNullOrWhiteSpace(Device.VariantName)
            ? $"{Device.ProductName} · {Device.VariantName}"
            : Device.ProductName)
        : string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanSubmit => !IsBusy && (!IsReportDefective || !string.IsNullOrWhiteSpace(Reason));

    public event Action<SerialImeiDto>? DeviceStatusChanged;
    public event EventHandler? RequestClose;

    public DeviceStatusDialogViewModel(ISerialDeviceRepository repository)
    {
        _repository = repository;
    }

    public DeviceStatusDialogViewModel(ISerialDeviceRepository repository, SerialImeiDto device, DeviceStatusMode mode)
        : this(repository)
    {
        Load(device, mode);
    }

    public void Load(SerialImeiDto device, DeviceStatusMode mode)
    {
        Device = device;
        Mode = mode;
        Reason = string.Empty;
        Note = string.Empty;
        ErrorMessage = null;
        IsBusy = false;
        NotifyStateChanged();
    }

    partial void OnModeChanged(DeviceStatusMode value)
    {
        NotifyStateChanged();
    }

    partial void OnReasonChanged(string value)
    {
        ConfirmCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnIsBusyChanged(bool value)
    {
        ConfirmCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsReportDefective));
        OnPropertyChanged(nameof(IsRestore));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(DeviceIdentifier));
        OnPropertyChanged(nameof(DeviceDescription));
        OnPropertyChanged(nameof(CanSubmit));
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    public async Task ConfirmAsync()
    {
        if (!CanSubmit || Device == null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            SerialDeviceMutationResult result;
            if (Mode == DeviceStatusMode.ReportDefective)
            {
                var request = new ReportDefectiveDeviceRequest
                {
                    Reason = Reason.Trim(),
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
                };
                result = await _repository.ReportDefectiveAsync(Device.Id, request);
            }
            else
            {
                var request = new RestoreDeviceRequest
                {
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
                };
                result = await _repository.RestoreAsync(Device.Id, request);
            }

            if (result.Succeeded && result.Data != null)
            {
                DeviceStatusChanged?.Invoke(result.Data);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = MapErrorMessage(result);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể kết nối máy chủ. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
            NotifyStateChanged();
        }
    }

    [RelayCommand]
    public void UsePresetReason(object? parameter)
    {
        if (parameter is string preset && !string.IsNullOrWhiteSpace(preset))
        {
            Reason = preset;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private static string MapErrorMessage(SerialDeviceMutationResult result) =>
        result.Failure switch
        {
            SerialDeviceFailureKind.Unauthorized => "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.",
            SerialDeviceFailureKind.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            SerialDeviceFailureKind.NotFound => result.Message ?? "Không tìm thấy thiết bị.",
            SerialDeviceFailureKind.Conflict => result.Message ?? "Xung đột trạng thái thiết bị.",
            SerialDeviceFailureKind.BadRequest => result.FieldErrors?.Values.SelectMany(v => v).FirstOrDefault()
                ?? result.Message ?? "Dữ liệu không hợp lệ.",
            _ => "Không thể kết nối máy chủ. Vui lòng thử lại."
        };
}
