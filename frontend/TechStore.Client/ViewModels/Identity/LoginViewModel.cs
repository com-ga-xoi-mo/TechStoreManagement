using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Identity;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Identity;

public enum LoginInputField
{
    Username,
    Password
}

public sealed partial class LoginViewModel : ObservableValidator
{
    private readonly Repositories.Identity.IAuthRepository _authRepository;
    private readonly ICurrentSession _session;
    private readonly IRootNavigationService _rootNavigation;
    private readonly IAuthModeProvider _authMode;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    private string username = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string usernameError = string.Empty;

    [ObservableProperty]
    private string passwordError = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isBrandingVisible = true;

    public bool IsFormEnabled => !IsLoading;

    public bool IsLoadingVisible => IsLoading;

    public bool IsErrorVisible => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsUsernameErrorVisible => !string.IsNullOrEmpty(UsernameError);

    public bool IsPasswordErrorVisible => !string.IsNullOrEmpty(PasswordError);

    public bool IsUnavailableVisible => _authMode.CurrentMode != AuthMode.ExplicitPreview;

    public event Action<LoginInputField>? FocusRequested;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsFormEnabled));
        OnPropertyChanged(nameof(IsLoadingVisible));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(IsErrorVisible));
    }

    partial void OnUsernameErrorChanged(string value) => OnPropertyChanged(nameof(IsUsernameErrorVisible));

    partial void OnPasswordErrorChanged(string value) => OnPropertyChanged(nameof(IsPasswordErrorVisible));

    partial void OnUsernameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) UsernameError = string.Empty;
    }

    partial void OnPasswordChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) PasswordError = string.Empty;
    }

    public LoginViewModel(
        Repositories.Identity.IAuthRepository authRepository,
        ICurrentSession session,
        IRootNavigationService rootNavigation,
        IAuthModeProvider authMode)
    {
        _authRepository = authRepository;
        _session = session;
        _rootNavigation = rootNavigation;
        _authMode = authMode;
    }

    [RelayCommand]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (IsLoading) return;

        ErrorMessage = string.Empty;
        ValidateAllProperties();
        UsernameError = string.IsNullOrWhiteSpace(Username) ? "Vui lòng nhập tên đăng nhập." : string.Empty;
        PasswordError = string.IsNullOrEmpty(Password) ? "Vui lòng nhập mật khẩu." : string.Empty;
        if (IsUsernameErrorVisible || IsPasswordErrorVisible)
        {
            FocusRequested?.Invoke(IsUsernameErrorVisible ? LoginInputField.Username : LoginInputField.Password);
            return;
        }

        if (_authMode.CurrentMode != AuthMode.ExplicitPreview)
        {
            ErrorMessage = "Đăng nhập chưa khả dụng vì Auth API chưa được tích hợp. Hãy chạy chế độ preview để thử giao diện.";
            Password = string.Empty;
            return;
        }

        IsLoading = true;

        try
        {
            var result = await _authRepository.LoginAsync(Username, Password, cancellationToken);
            
            if (result.Status == LoginResultStatus.Success && result.Session != null)
            {
                Password = string.Empty;
                _session.SetPreview();
                _rootNavigation.ShowShell();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Đăng nhập thất bại. Vui lòng thử lại.";
                Password = string.Empty;
            }
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Đã hủy đăng nhập.";
            Password = string.Empty;
        }
        catch
        {
            ErrorMessage = "Đã xảy ra lỗi không xác định. Vui lòng thử lại sau.";
            Password = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
