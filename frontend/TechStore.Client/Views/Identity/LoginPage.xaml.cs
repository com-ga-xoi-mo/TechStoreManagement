using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Identity;

namespace TechStore.Client.Views.Identity;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        this.InitializeComponent();

        // Focus management
        this.Loaded += (s, e) => UsernameBox.Focus(FocusState.Programmatic);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.FocusRequested += ViewModel_FocusRequested;
        this.Unloaded += (s, e) =>
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.FocusRequested -= ViewModel_FocusRequested;
            ViewModel.Password = string.Empty;
            PasswordBox.Password = string.Empty;
        };
        
        // Handle window resizing for responsive layout
        this.SizeChanged += LoginPage_SizeChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.Password) && PasswordBox.Password != ViewModel.Password)
        {
            PasswordBox.Password = ViewModel.Password;
        }

        if (e.PropertyName == nameof(LoginViewModel.IsFormEnabled) && ViewModel.IsFormEnabled)
        {
            // If form was disabled (loading) and re-enabled (rejected), set focus
            if (string.IsNullOrEmpty(ViewModel.Password))
            {
                PasswordBox.Focus(FocusState.Programmatic);
            }
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password != ViewModel.Password)
        {
            ViewModel.Password = PasswordBox.Password;
        }
    }

    private void ViewModel_FocusRequested(LoginInputField field)
    {
        if (field == LoginInputField.Username) UsernameBox.Focus(FocusState.Programmatic);
        else PasswordBox.Focus(FocusState.Programmatic);
    }

    private void LoginPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Hide left branding region if window is too narrow
        ViewModel.IsBrandingVisible = e.NewSize.Width > 768;
        BrandColumn.MinWidth = ViewModel.IsBrandingVisible ? 300 : 0;
        BrandColumn.Width = ViewModel.IsBrandingVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }
}
