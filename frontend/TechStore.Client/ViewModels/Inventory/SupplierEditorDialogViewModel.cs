using System.Net.Mail;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class SupplierEditorDialogViewModel(ISupplierRepository supplierRepository) : ObservableObject
{
    private static readonly Regex PhoneRegex = new(@"^[0-9 +().-]{1,30}$", RegexOptions.Compiled);

    [ObservableProperty] private Guid? _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _contactName;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _address;

    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private string? _contactNameError;
    [ObservableProperty] private string? _phoneError;
    [ObservableProperty] private string? _emailError;
    [ObservableProperty] private string? _addressError;
    [ObservableProperty] private string? _generalError;
    [ObservableProperty] private bool _isBusy;

    public bool IsEditing => Id.HasValue;
    public string Title => IsEditing ? "Chỉnh sửa nhà cung cấp" : "Thêm nhà cung cấp mới";
    public string PrimaryButtonText => IsEditing ? "Lưu thay đổi" : "Tạo mới";

    public event Action<SupplierItem>? SupplierSaved;
    public event EventHandler? RequestClose;

    public void Load(SupplierItem? item)
    {
        ClearErrors();
        if (item != null)
        {
            Id = item.Id;
            Name = item.Name;
            ContactName = item.ContactName;
            Phone = item.Phone;
            Email = item.Email;
            Address = item.Address;
        }
        else
        {
            Id = null;
            Name = string.Empty;
            ContactName = null;
            Phone = string.Empty;
            Email = null;
            Address = null;
        }

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PrimaryButtonText));
    }

    private void ClearErrors()
    {
        NameError = null;
        ContactNameError = null;
        PhoneError = null;
        EmailError = null;
        AddressError = null;
        GeneralError = null;
    }

    private bool ValidateInput()
    {
        ClearErrors();
        var isValid = true;

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = "Tên nhà cung cấp là bắt buộc.";
            isValid = false;
        }
        else if (Name.Trim().Length > 200)
        {
            NameError = "Tên nhà cung cấp không được vượt quá 200 ký tự.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            PhoneError = "Số điện thoại là bắt buộc.";
            isValid = false;
        }
        else if (!PhoneRegex.IsMatch(Phone.Trim()))
        {
            PhoneError = "Số điện thoại không hợp lệ (chỉ chứa chữ số và các ký tự + - . ( ) khoảng trắng, tối đa 30 ký tự).";
            isValid = false;
        }

        if (!string.IsNullOrWhiteSpace(Email))
        {
            var trimmedEmail = Email.Trim();
            if (trimmedEmail.Length > 255 || !MailAddress.TryCreate(trimmedEmail, out _))
            {
                EmailError = "Địa chỉ email không hợp lệ.";
                isValid = false;
            }
        }

        if (ContactName != null && ContactName.Trim().Length > 100)
        {
            ContactNameError = "Tên người liên hệ không được vượt quá 100 ký tự.";
            isValid = false;
        }

        if (Address != null && Address.Trim().Length > 500)
        {
            AddressError = "Địa chỉ không được vượt quá 500 ký tự.";
            isValid = false;
        }

        return isValid;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsBusy) return;
        if (!ValidateInput()) return;

        IsBusy = true;
        GeneralError = null;

        try
        {
            var request = new SaveSupplierRequest
            {
                Name = Name.Trim(),
                ContactName = string.IsNullOrWhiteSpace(ContactName) ? null : ContactName.Trim(),
                Phone = Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim()
            };

            var result = IsEditing
                ? await supplierRepository.UpdateAsync(Id!.Value, request)
                : await supplierRepository.CreateAsync(request);

            if (result.Succeeded)
            {
                SupplierSaved?.Invoke(result.Data!);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                if (result.Failure == SupplierFailureKind.Conflict)
                {
                    // The server explains which conflict it is (duplicate name or the reserved system name).
                    NameError = result.Message ?? "Tên nhà cung cấp đã tồn tại.";
                }
                else if (result.Failure == SupplierFailureKind.BadRequest && result.FieldErrors != null)
                {
                    if (result.FieldErrors.TryGetValue("name", out var nameErrs) && nameErrs.Count > 0)
                    {
                        NameError = nameErrs[0];
                    }

                    if (result.FieldErrors.TryGetValue("phone", out var phoneErrs) && phoneErrs.Count > 0)
                    {
                        PhoneError = phoneErrs[0];
                    }

                    if (result.FieldErrors.TryGetValue("email", out var emailErrs) && emailErrs.Count > 0)
                    {
                        EmailError = emailErrs[0];
                    }

                    if (result.FieldErrors.TryGetValue("contactName", out var contactErrs) && contactErrs.Count > 0)
                    {
                        ContactNameError = contactErrs[0];
                    }

                    if (result.FieldErrors.TryGetValue("address", out var addrErrs) && addrErrs.Count > 0)
                    {
                        AddressError = addrErrs[0];
                    }
                }
                else
                {
                    GeneralError = result.Failure switch
                    {
                        SupplierFailureKind.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                        SupplierFailureKind.Forbidden => "Bạn không có quyền thêm hoặc sửa nhà cung cấp.",
                        SupplierFailureKind.NotFound => "Nhà cung cấp không còn tồn tại.",
                        SupplierFailureKind.BadRequest => result.Message ?? "Thông tin nhà cung cấp không hợp lệ.",
                        _ => "Không thể kết nối máy chủ. Vui lòng thử lại."
                    };
                }
            }
        }
        catch (Exception)
        {
            GeneralError = "Không thể kết nối máy chủ. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
