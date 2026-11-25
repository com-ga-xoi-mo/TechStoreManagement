using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Vouchers;
using TechStore.Client.Repositories.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Vouchers;

/// <summary>Create or edit one voucher. The checks repeat the server rules to answer at once; the server stays the authority.</summary>
public sealed partial class VoucherEditorDialogViewModel : ObservableObject
{
    private const int MaxDescriptionLength = 500;
    private static readonly Regex CodePattern = new("^[A-Za-z0-9_-]{3,32}$", RegexOptions.Compiled);
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly IVoucherRepository _vouchers;
    private readonly TimeProvider _timeProvider;
    private VoucherItem? _editing;

    public static IReadOnlyList<string> TypeOptions { get; } = ["Phần trăm (%)", "Số tiền cố định (₫)"];

    [ObservableProperty] private string code = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private int discountTypeIndex;
    [ObservableProperty] private string discountValue = string.Empty;
    [ObservableProperty] private string minOrderAmount = string.Empty;
    [ObservableProperty] private string maxDiscountAmount = string.Empty;
    [ObservableProperty] private string usageLimit = string.Empty;
    [ObservableProperty] private DateTimeOffset? expiresAt;
    [ObservableProperty] private string codeError = string.Empty;
    [ObservableProperty] private string descriptionError = string.Empty;
    [ObservableProperty] private string discountValueError = string.Empty;
    [ObservableProperty] private string minOrderAmountError = string.Empty;
    [ObservableProperty] private string maxDiscountAmountError = string.Empty;
    [ObservableProperty] private string usageLimitError = string.Empty;
    [ObservableProperty] private string expiresAtError = string.Empty;
    [ObservableProperty] private string formError = string.Empty;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private VoucherItem? savedVoucher;

    /// <summary>Raised when the dialog should close: after a successful save, or on cancel.</summary>
    public event EventHandler? RequestClose;

    public VoucherEditorDialogViewModel(IVoucherRepository vouchers, TimeProvider? timeProvider = null)
    {
        _vouchers = vouchers ?? throw new ArgumentNullException(nameof(vouchers));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsEdit => _editing is not null;
    public string Title => _editing is { } voucher ? $"Sửa voucher {voucher.Code}" : "Tạo voucher";
    public string SaveButtonText => IsSaving ? "Đang lưu..." : IsEdit ? "Lưu thay đổi" : "Lưu voucher";
    public bool IsCodeEditable => !IsEdit;
    public bool IsTypeEditable => !IsEdit;
    public bool IsPercentage => DiscountTypeIndex == 0;
    public bool ShowMaxDiscount => IsPercentage;
    public bool HasFieldErrors => !string.IsNullOrEmpty(CodeError) || !string.IsNullOrEmpty(DescriptionError)
        || !string.IsNullOrEmpty(DiscountValueError) || !string.IsNullOrEmpty(MinOrderAmountError)
        || !string.IsNullOrEmpty(MaxDiscountAmountError) || !string.IsNullOrEmpty(UsageLimitError)
        || !string.IsNullOrEmpty(ExpiresAtError);
    public bool HasFormError => !string.IsNullOrEmpty(FormError);

    /// <summary>Starts the form for a new voucher (null) or for editing an existing one.</summary>
    public void Initialize(VoucherItem? voucher)
    {
        _editing = voucher;
        ClearErrors();
        SavedVoucher = null;
        IsSaving = false;
        Code = voucher?.Code ?? string.Empty;
        Description = voucher?.Description ?? string.Empty;
        DiscountTypeIndex = voucher is { IsPercentage: false } ? 1 : 0;
        DiscountValue = voucher is null ? string.Empty : FormatPercent(voucher);
        MinOrderAmount = voucher is null ? string.Empty : FormatWhole(voucher.MinOrderAmount);
        MaxDiscountAmount = voucher?.MaxDiscountAmount is { } cap ? FormatWhole(cap) : string.Empty;
        UsageLimit = voucher is null ? string.Empty : voucher.UsageLimit.ToString(CultureInfo.InvariantCulture);
        ExpiresAt = voucher is null ? null : new DateTimeOffset(voucher.ExpiresAt.ToLocalTime().Date);
        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(IsCodeEditable));
        OnPropertyChanged(nameof(IsTypeEditable));
        OnPropertyChanged(nameof(IsPercentage));
        OnPropertyChanged(nameof(ShowMaxDiscount));
    }

    private bool CanSave() => !IsSaving;

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ClearErrors();
        var input = Validate();
        if (input is null) return;

        IsSaving = true;
        try
        {
            SavedVoucher = _editing is { } voucher
                ? await _vouchers.UpdateAsync(voucher.Id, new UpdateVoucherRequest
                {
                    Description = input.Description,
                    DiscountValue = input.DiscountValue,
                    MinOrderAmount = input.MinOrderAmount,
                    MaxDiscountAmount = input.MaxDiscountAmount,
                    UsageLimit = input.UsageLimit,
                    ExpiresAt = input.ExpiresAtUtc
                }, cancellationToken)
                : await _vouchers.CreateAsync(new CreateVoucherRequest
                {
                    Code = input.Code,
                    Description = input.Description,
                    DiscountType = input.DiscountType,
                    DiscountValue = input.DiscountValue,
                    MinOrderAmount = input.MinOrderAmount,
                    MaxDiscountAmount = input.MaxDiscountAmount,
                    UsageLimit = input.UsageLimit,
                    ExpiresAt = input.ExpiresAtUtc
                }, cancellationToken);
            RequestClose?.Invoke(this, EventArgs.Empty);
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
            FormError = "Không thể lưu voucher. Hãy thử lại.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private sealed record Input(
        string Code,
        string? Description,
        string DiscountType,
        decimal DiscountValue,
        decimal MinOrderAmount,
        decimal? MaxDiscountAmount,
        int UsageLimit,
        DateTime ExpiresAtUtc);

    private Input? Validate()
    {
        var trimmedCode = Code.Trim();
        if (!IsEdit)
        {
            if (trimmedCode.Length == 0) CodeError = "Vui lòng nhập mã voucher.";
            else if (!CodePattern.IsMatch(trimmedCode)) CodeError = "Mã gồm 3–32 ký tự: chữ, số, '-' hoặc '_'.";
        }

        var discount = ParseDiscountValue();
        var min = ParseWholeAmount(MinOrderAmount, allowBlank: true, out var minError);
        MinOrderAmountError = minError;
        decimal? max = null;
        if (IsPercentage)
        {
            max = ParseWholeAmount(MaxDiscountAmount, allowBlank: true, out var maxError, blankAsNull: true);
            MaxDiscountAmountError = maxError;
        }

        var limit = ParseUsageLimit();
        var expiresUtc = ParseExpiry();
        var text = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        if (text is { Length: > MaxDescriptionLength }) DescriptionError = $"Mô tả tối đa {MaxDescriptionLength} ký tự.";

        if (HasFieldErrors || discount is null || min is null || limit is null || expiresUtc is null) return null;
        return new Input(trimmedCode.ToUpperInvariant(), text, IsPercentage ? VoucherItem.Percentage : VoucherItem.FixedAmount,
            discount.Value, min.Value, max, limit.Value, expiresUtc.Value);
    }

    private decimal? ParseDiscountValue()
    {
        if (string.IsNullOrWhiteSpace(DiscountValue))
        {
            DiscountValueError = "Vui lòng nhập giá trị giảm.";
            return null;
        }

        if (!TryParseNumber(DiscountValue, percent: IsPercentage, out var value))
        {
            DiscountValueError = "Giá trị giảm không hợp lệ.";
            return null;
        }

        if (value <= 0) DiscountValueError = "Giá trị giảm phải lớn hơn 0.";
        else if (IsPercentage && value > 100) DiscountValueError = "Phần trăm giảm không vượt quá 100.";
        else if (IsPercentage && decimal.Round(value, 2) != value) DiscountValueError = "Phần trăm giảm có tối đa 2 chữ số thập phân.";
        else if (!IsPercentage && value != decimal.Truncate(value)) DiscountValueError = "Số tiền giảm phải là số nguyên đồng.";
        return string.IsNullOrEmpty(DiscountValueError) ? value : null;
    }

    private static decimal? ParseWholeAmount(string text, bool allowBlank, out string error, bool blankAsNull = false)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            if (allowBlank) return blankAsNull ? null : 0m;
            error = "Vui lòng nhập số tiền.";
            return null;
        }

        if (!TryParseNumber(text, percent: false, out var value) || value < 0 || value != decimal.Truncate(value))
        {
            error = "Nhập số nguyên đồng không âm.";
            return null;
        }

        return value;
    }

    private int? ParseUsageLimit()
    {
        if (string.IsNullOrWhiteSpace(UsageLimit))
        {
            UsageLimitError = "Vui lòng nhập số lượt dùng.";
            return null;
        }

        if (!TryParseNumber(UsageLimit, percent: false, out var value) || value != decimal.Truncate(value) || value > int.MaxValue)
        {
            UsageLimitError = "Số lượt dùng phải là số nguyên.";
            return null;
        }

        if (value < 1) UsageLimitError = "Số lượt dùng phải ít nhất 1.";
        else if (_editing is { } voucher && value < voucher.UsedCount + voucher.ReservedCount)
            UsageLimitError = $"Số lượt dùng không được nhỏ hơn {voucher.UsedCount + voucher.ReservedCount} lượt đã dùng hoặc đang giữ.";
        return string.IsNullOrEmpty(UsageLimitError) ? (int)value : null;
    }

    private DateTime? ParseExpiry()
    {
        if (ExpiresAt is not { } date)
        {
            ExpiresAtError = "Vui lòng chọn ngày hết hạn.";
            return null;
        }

        // The voucher works through the chosen day, in the shop's local time.
        var endOfDay = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Local).ToUniversalTime();
        if (endOfDay <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            ExpiresAtError = "Ngày hết hạn phải ở tương lai.";
            return null;
        }

        return endOfDay;
    }

    /// <summary>
    /// Percentages take '.' or ',' as the decimal mark. Amounts and counts follow the Vietnamese way:
    /// '.' groups thousands ("2.000.000") and ',' is the decimal mark, so "1000,5" is not a whole number.
    /// </summary>
    private static bool TryParseNumber(string text, bool percent, out decimal value)
    {
        var cleaned = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        cleaned = percent
            ? cleaned.Replace(',', '.')
            : cleaned.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }

    private void ApplyApiError(ApiException exception)
    {
        foreach (var (field, messages) in exception.FieldErrors)
        {
            var message = messages.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(message)) continue;
            switch (field.ToLowerInvariant())
            {
                case "code": CodeError = message; break;
                case "description": DescriptionError = message; break;
                case "discountvalue": DiscountValueError = message; break;
                case "minorderamount": MinOrderAmountError = message; break;
                case "maxdiscountamount": MaxDiscountAmountError = message; break;
                case "usagelimit": UsageLimitError = message; break;
                case "expiresat": ExpiresAtError = message; break;
                default: FormError = message; break;
            }
        }

        if (exception.StatusCode == 409) CodeError = "Mã voucher đã tồn tại.";
        else if (exception.StatusCode == 401) FormError = "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.";
        else if (exception.StatusCode == 403) FormError = "Bạn không có quyền chỉnh sửa voucher.";
        else if (exception.StatusCode == 404) FormError = "Voucher không còn tồn tại. Hãy đóng hộp thoại và tải lại danh sách.";
        else if (exception.Kind == ApiErrorKind.Network) FormError = "Không thể kết nối máy chủ. Hãy thử lại.";
        else if (!HasFieldErrors && string.IsNullOrEmpty(FormError))
            FormError = exception.StatusCode == 400
                ? "Không thể lưu voucher. Hãy kiểm tra thông tin rồi thử lại."
                : "Không thể lưu voucher. Hãy thử lại.";
    }

    private void ClearErrors()
    {
        CodeError = DescriptionError = DiscountValueError = MinOrderAmountError = MaxDiscountAmountError = string.Empty;
        UsageLimitError = ExpiresAtError = FormError = string.Empty;
    }

    private static string FormatPercent(VoucherItem voucher) => voucher.IsPercentage
        ? voucher.DiscountValue.ToString("0.##", Vietnamese)
        : FormatWhole(voucher.DiscountValue);

    private static string FormatWhole(decimal value) => decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture);

    partial void OnDiscountTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsPercentage));
        OnPropertyChanged(nameof(ShowMaxDiscount));
    }

    partial void OnIsSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(SaveButtonText));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnFormErrorChanged(string value) => OnPropertyChanged(nameof(HasFormError));
    partial void OnCodeErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnDescriptionErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnDiscountValueErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnMinOrderAmountErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnMaxDiscountAmountErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnUsageLimitErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
    partial void OnExpiresAtErrorChanged(string value) => OnPropertyChanged(nameof(HasFieldErrors));
}
