using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Customers;

public sealed partial class CustomerFormViewModel : PageViewModel
{
    private readonly ICustomerRepository _customers;
    private Guid? _customerId;
    private string _customerDisplayName = string.Empty;
    private string _membershipTier = string.Empty;
    private int _loyaltyPoints;

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string phone = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string dateOfBirth = string.Empty;
    [ObservableProperty] private DateTimeOffset? dateOfBirthDate;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string fullNameError = string.Empty;
    [ObservableProperty] private string phoneError = string.Empty;
    [ObservableProperty] private string emailError = string.Empty;
    [ObservableProperty] private string dateOfBirthError = string.Empty;
    [ObservableProperty] private string addressError = string.Empty;
    [ObservableProperty] private string formError = string.Empty;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private CustomerProfile? savedProfile;

    public bool IsEdit => _customerId.HasValue;
    public string TierCode => _membershipTier;
    public string Title => IsEdit ? "Chỉnh sửa thông tin khách hàng" : "Thêm khách hàng";
    public string Subtitle => IsEdit
        ? $"Cập nhật thông tin định danh cho {_customerDisplayName}"
        : "Tạo hồ sơ khách hàng mới cho TechStore";
    public bool ShowMembershipSummary => IsEdit;
    public string TierLabel => _membershipTier switch
    {
        "Gold" => "Hạng Vàng",
        "Silver" => "Hạng Bạc",
        "Platinum" => "Hạng Bạch kim",
        _ => "Hạng Tiêu chuẩn"
    };
    public string LoyaltyPointsLabel => $"{_loyaltyPoints.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} điểm";
    public string SaveButtonText => IsSaving ? "Đang lưu..." : IsEdit ? "Lưu thay đổi" : "Lưu khách hàng";

    public CustomerFormViewModel(ICustomerRepository customers, CustomerProfile? customer = null)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        Initialize(customer);
    }

    public void Initialize(CustomerProfile? customer)
    {
        _customerId = customer?.Id;
        _customerDisplayName = customer?.FullName ?? string.Empty;
        _membershipTier = customer?.Tier ?? string.Empty;
        _loyaltyPoints = customer?.LoyaltyPoints ?? 0;
        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ShowMembershipSummary));
        OnPropertyChanged(nameof(TierCode));
        OnPropertyChanged(nameof(TierLabel));
        OnPropertyChanged(nameof(LoyaltyPointsLabel));
        OnPropertyChanged(nameof(SaveButtonText));
        FullName = customer?.FullName ?? string.Empty;
        Phone = customer?.Phone ?? string.Empty;
        Email = customer?.Email ?? string.Empty;
        DateOfBirthDate = customer?.DateOfBirth is { } birthDate
            ? new DateTimeOffset(birthDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
        DateOfBirth = customer?.DateOfBirth?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        Address = customer?.Address ?? string.Empty;
        ClearErrors();
        SavedProfile = null;
        IsSaving = false;
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Idle;
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ClearErrors();
        if (string.IsNullOrWhiteSpace(FullName)) FullNameError = "Vui lòng nhập họ tên.";
        if (string.IsNullOrWhiteSpace(Phone)) PhoneError = "Vui lòng nhập số điện thoại.";
        if (!string.IsNullOrWhiteSpace(Phone)
            && (Phone.Trim().Length > 10 || Phone.Trim().Any(character => character is < '0' or > '9')))
            PhoneError = "S\u1ED1 \u0111i\u1EC7n tho\u1EA1i ch\u1EC9 g\u1ED3m ch\u1EEF s\u1ED1 v\u00E0 t\u1ED1i \u0111a 10 s\u1ED1.";

        DateOnly? birthDate = null;
        if (!string.IsNullOrWhiteSpace(DateOfBirth))
        {
            if (DateOnly.TryParseExact(DateOfBirth, ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
            {
                birthDate = parsed;
                DateOfBirthDate = new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                DateOfBirth = parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            else
                DateOfBirthError = "Ngày sinh không hợp lệ. Nhập theo định dạng dd/mm/yyyy hoặc chọn trên lịch.";
        }
        if (birthDate > DateOnly.FromDateTime(DateTime.Today))
            DateOfBirthError = "Ng\u00E0y sinh kh\u00F4ng \u0111\u01B0\u1EE3c n\u1EB1m trong t\u01B0\u01A1ng lai.";
        if (HasFieldErrors) return;

        IsSaving = true;
        try
        {
            var request = new CreateCustomerRequest
            {
                FullName = FullName.Trim(),
                Phone = Phone.Trim(),
                Email = NullIfWhiteSpace(Email),
                DateOfBirth = birthDate,
                Address = NullIfWhiteSpace(Address)
            };
            SavedProfile = IsEdit
                ? await _customers.UpdateAsync(_customerId!.Value, new UpdateCustomerRequest
                {
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Email = request.Email,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address
                }, cancellationToken)
                : await _customers.CreateAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ApiException exception)
        {
            ApplyApiError(exception);
        }
        catch
        {
            FormError = "Không thể lưu khách hàng. Hãy thử lại.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    public bool HasFieldErrors => !string.IsNullOrEmpty(FullNameError) || !string.IsNullOrEmpty(PhoneError)
        || !string.IsNullOrEmpty(EmailError) || !string.IsNullOrEmpty(DateOfBirthError)
        || !string.IsNullOrEmpty(AddressError);

    private void ApplyApiError(ApiException exception)
    {
        foreach (var (field, messages) in exception.FieldErrors)
        {
            var message = messages.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(message)) continue;
            switch (field.ToLowerInvariant())
            {
                case "fullname": FullNameError = message; break;
                case "phone": PhoneError = message; break;
                case "email": EmailError = message; break;
                case "dateofbirth": DateOfBirthError = message; break;
                case "address": AddressError = message; break;
                default: FormError = message; break;
            }
        }

        if (exception.FieldErrors.TryGetValue("dateOfBirth", out var dateErrors)
            && dateErrors.Any(message => message.Contains("future", StringComparison.OrdinalIgnoreCase)))
            DateOfBirthError = "Ng\u00E0y sinh kh\u00F4ng \u0111\u01B0\u1EE3c n\u1EB1m trong t\u01B0\u01A1ng lai.";

        if (exception.StatusCode == 409 && string.IsNullOrEmpty(PhoneError))
            PhoneError = "Số điện thoại đã được sử dụng.";
        else if (exception.StatusCode == 401)
            FormError = "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.";
        else if (exception.StatusCode == 403)
            FormError = "Bạn không có quyền chỉnh sửa khách hàng.";
        else if (!HasFieldErrors && string.IsNullOrEmpty(FormError))
            FormError = "Không thể lưu khách hàng. Hãy kiểm tra thông tin rồi thử lại.";
    }

    private void ClearErrors()
    {
        FullNameError = PhoneError = EmailError = DateOfBirthError = AddressError = FormError = string.Empty;
    }

    partial void OnDateOfBirthChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            DateOfBirthDate = null;
            DateOfBirthError = string.Empty;
        }
        else if (DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out var parsed))
        {
            DateOfBirthDate = new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            DateOfBirthError = string.Empty;
        }
    }

    partial void OnDateOfBirthDateChanged(DateTimeOffset? value)
    {
        if (value is null)
        {
            if (!string.IsNullOrEmpty(DateOfBirth)) DateOfBirth = string.Empty;
            return;
        }

        var parsed = DateOnly.FromDateTime(value.Value.DateTime);
        var formatted = parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        if (!string.Equals(DateOfBirth, formatted, StringComparison.Ordinal)) DateOfBirth = formatted;
        DateOfBirthError = string.Empty;
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    partial void OnFullNameErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnPhoneErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnEmailErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnDateOfBirthErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnAddressErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(SaveButtonText));
}
