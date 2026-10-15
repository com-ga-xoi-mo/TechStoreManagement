using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Navigation;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Identity;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthRepository _authRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private string username = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private bool rememberMe = true;
    [ObservableProperty] private bool isLoggingIn;
    [ObservableProperty] private bool isPasswordVisible;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string usernameError = string.Empty;
    [ObservableProperty] private string passwordError = string.Empty;

    public LoginViewModel(IAuthRepository authRepository, INavigationService navigationService)
    {
        _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    public async Task LoginAsync()
    {
        UsernameError = string.Empty;
        PasswordError = string.Empty;
        ErrorMessage = string.Empty;
        HasError = false;

        var valid = true;
        if (string.IsNullOrWhiteSpace(Username))
        {
            UsernameError = "Vui lòng nhập tên đăng nhập";
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Vui lòng nhập mật khẩu";
            valid = false;
        }

        if (!valid)
        {
            return;
        }

        IsLoggingIn = true;
        try
        {
            var request = new LoginRequest
            {
                Username = Username.Trim(),
                Password = Password,
                RememberMe = RememberMe
            };

            var response = await _authRepository.LoginAsync(request);
            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                // Navigate to Dashboard upon successful login
                _navigationService.Navigate(ShellDestination.Dashboard);
            }
            else
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                ? "Tên đăng nhập hoặc mật khẩu không đúng"
                : "Không thể kết nối đến máy chủ. Hãy thử lại.";
            HasError = true;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }
}
