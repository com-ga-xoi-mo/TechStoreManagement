using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Identity;
using Windows.UI;

namespace TechStore.Client.Views.Identity;

public sealed class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    private readonly TextBox _usernameBox;
    private readonly PasswordBox _passwordBox;
    private readonly TextBox _passwordRevealBox;
    private readonly TextBlock _usernameErrorText;
    private readonly TextBlock _passwordErrorText;
    private readonly Border _errorBanner;
    private readonly TextBlock _errorBannerText;
    private readonly Button _loginButton;
    private readonly ProgressRing _loadingRing;
    private readonly TextBlock _loginButtonText;

    public LoginPage(LoginViewModel viewModel)
    {
        ViewModel = viewModel;
        Background = new SolidColorBrush(StitchTokens.SurfaceCanvas);

        // Root container: 2 columns (Left Branding Panel, Right Form Panel)
        var rootGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(480, GridUnitType.Pixel) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            }
        };

        // LEFT COLUMN: Branding Panel (Stitch #0B1528 -> #0F1E3D -> #0A2558)
        var leftPanel = BuildBrandingPanel();

        // RIGHT COLUMN: Login Form Card
        var formPanel = BuildFormPanel(out _usernameBox, out _passwordBox, out _passwordRevealBox,
            out _usernameErrorText, out _passwordErrorText, out _errorBanner, out _errorBannerText,
            out _loginButton, out _loadingRing, out _loginButtonText);

        Grid.SetColumn(leftPanel, 0);
        Grid.SetColumn(formPanel, 1);
        rootGrid.Children.Add(leftPanel);
        rootGrid.Children.Add(formPanel);

        Content = rootGrid;

        // View model bindings
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsLoggingIn))
            {
                _loadingRing.IsActive = ViewModel.IsLoggingIn;
                _loadingRing.Visibility = ViewModel.IsLoggingIn ? Visibility.Visible : Visibility.Collapsed;
                _loginButtonText.Text = ViewModel.IsLoggingIn ? "Đang xử lý..." : "Đăng nhập";
                _loginButton.IsEnabled = !ViewModel.IsLoggingIn;
            }
            if (e.PropertyName == nameof(ViewModel.HasError))
            {
                _errorBanner.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
            }
            if (e.PropertyName == nameof(ViewModel.ErrorMessage))
            {
                _errorBannerText.Text = ViewModel.ErrorMessage;
            }
            if (e.PropertyName == nameof(ViewModel.UsernameError))
            {
                _usernameErrorText.Text = ViewModel.UsernameError;
                _usernameErrorText.Visibility = string.IsNullOrEmpty(ViewModel.UsernameError) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (e.PropertyName == nameof(ViewModel.PasswordError))
            {
                _passwordErrorText.Text = ViewModel.PasswordError;
                _passwordErrorText.Visibility = string.IsNullOrEmpty(ViewModel.PasswordError) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (e.PropertyName == nameof(ViewModel.IsPasswordVisible))
            {
                if (ViewModel.IsPasswordVisible)
                {
                    _passwordRevealBox.Text = _passwordBox.Password;
                    _passwordRevealBox.Visibility = Visibility.Visible;
                    _passwordBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _passwordBox.Password = _passwordRevealBox.Text;
                    _passwordBox.Visibility = Visibility.Visible;
                    _passwordRevealBox.Visibility = Visibility.Collapsed;
                }
            }
        };
    }

    private static UIElement BuildBrandingPanel()
    {
        var panel = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x0B, 0x15, 0x28), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x0F, 0x1E, 0x3D), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x0A, 0x25, 0x58), Offset = 1 }
                }
            },
            Padding = new Thickness(48),
            Child = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto }
                }
            }
        };

        var grid = (Grid)panel.Child;

        // Header logo
        var logoStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Children =
            {
                new Border
                {
                    Width = 44,
                    Height = 44,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(StitchTokens.Primary),
                    Child = new FontIcon
                    {
                        Glyph = "\uE7F8",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 22,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                },
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = "TechStore", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) },
                        new TextBlock { Text = "Hệ thống quản lý bán lẻ", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF)) }
                    }
                }
            }
        };
        Grid.SetRow(logoStack, 0);
        grid.Children.Add(logoStack);

        // Center: Highlights
        var centerStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 20,
            Children =
            {
                new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x66, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x60, 0xA5, 0xFA)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(20),
                    Padding = new Thickness(12, 6, 12, 6),
                    Child = new TextBlock
                    {
                        Text = "● Nền tảng vận hành POS & Kho đa kênh",
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x93, 0xC5, 0xFD))
                    }
                },
                new TextBlock
                {
                    Text = "Quản lý bán lẻ thiết bị công nghệ",
                    FontSize = 26,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = "Hệ thống tích hợp theo dõi tồn kho Serial/IMEI, quản lý bán hàng tại quầy và chăm sóc khách hàng chuyên nghiệp.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xCB, 0xD5, 0xE1)),
                    TextWrapping = TextWrapping.Wrap
                },
                new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(20),
                    Child = new StackPanel
                    {
                        Spacing = 14,
                        Children =
                        {
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 12,
                                Children =
                                {
                                    new FontIcon { Glyph = "\uE890", Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0xA5, 0xFA)), FontSize = 16 },
                                    new TextBlock { Text = "Kiểm soát 15 số IMEI chính xác theo từng máy", FontSize = 13, Foreground = new SolidColorBrush(Colors.White) }
                                }
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 12,
                                Children =
                                {
                                    new FontIcon { Glyph = "\uE8C7", Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x34, 0xD3, 0x99)), FontSize = 16 },
                                    new TextBlock { Text = "Thanh toán nhanh: Tiền mặt, Thẻ POS & VietQR", FontSize = 13, Foreground = new SolidColorBrush(Colors.White) }
                                }
                            }
                        }
                    }
                }
            }
        };
        Grid.SetRow(centerStack, 1);
        grid.Children.Add(centerStack);

        // Footer version
        var footerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = "Phiên bản Desktop v2.4 · Bảo mật xác thực doanh nghiệp", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)) }
            }
        };
        Grid.SetRow(footerStack, 2);
        grid.Children.Add(footerStack);

        return panel;
    }

    private UIElement BuildFormPanel(
        out TextBox usernameBox,
        out PasswordBox passwordBox,
        out TextBox passwordRevealBox,
        out TextBlock usernameErrorText,
        out TextBlock passwordErrorText,
        out Border errorBanner,
        out TextBlock errorBannerText,
        out Button loginButton,
        out ProgressRing loadingRing,
        out TextBlock loginButtonText)
    {
        // 440px form card centered
        var uBox = new TextBox
        {
            PlaceholderText = "Nhập tên đăng nhập hoặc mã nhân viên",
            Height = 40,
            CornerRadius = new CornerRadius(8)
        };
        uBox.TextChanged += (s, e) => ViewModel.Username = uBox.Text;
        usernameBox = uBox;

        usernameErrorText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ErrorRed),
            Visibility = Visibility.Collapsed
        };

        var pBox = new PasswordBox
        {
            PlaceholderText = "Nhập mật khẩu của bạn",
            Height = 40,
            CornerRadius = new CornerRadius(8)
        };
        pBox.PasswordChanged += (s, e) => ViewModel.Password = pBox.Password;
        passwordBox = pBox;

        var rBox = new TextBox
        {
            PlaceholderText = "Nhập mật khẩu của bạn",
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Visibility = Visibility.Collapsed
        };
        rBox.TextChanged += (s, e) => ViewModel.Password = rBox.Text;
        passwordRevealBox = rBox;

        passwordErrorText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ErrorRed),
            Visibility = Visibility.Collapsed
        };

        var togglePwdBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE7B3", FontSize = 14 },
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        togglePwdBtn.Click += (s, e) => ViewModel.TogglePasswordVisibilityCommand.Execute(null);

        var pwdGrid = new Grid();
        pwdGrid.Children.Add(passwordBox);
        pwdGrid.Children.Add(passwordRevealBox);
        pwdGrid.Children.Add(togglePwdBtn);

        var rememberCheckBox = new CheckBox
        {
            Content = "Ghi nhớ đăng nhập",
            IsChecked = ViewModel.RememberMe
        };
        rememberCheckBox.Checked += (s, e) => ViewModel.RememberMe = true;
        rememberCheckBox.Unchecked += (s, e) => ViewModel.RememberMe = false;

        // Error banner
        errorBannerText = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(StitchTokens.ErrorText),
            VerticalAlignment = VerticalAlignment.Center
        };

        errorBanner = new Border
        {
            Background = new SolidColorBrush(StitchTokens.ErrorBg),
            BorderBrush = new SolidColorBrush(StitchTokens.ErrorBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Visibility = Visibility.Collapsed,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE783", Foreground = new SolidColorBrush(StitchTokens.ErrorRed), FontSize = 16 },
                    errorBannerText
                }
            }
        };

        // Submit Button
        loadingRing = new ProgressRing
        {
            Width = 16,
            Height = 16,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(Colors.White)
        };

        loginButtonText = new TextBlock
        {
            Text = "Đăng nhập",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White)
        };

        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { loadingRing, loginButtonText }
        };

        loginButton = new Button
        {
            Content = buttonStack,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(StitchTokens.Primary),
            CornerRadius = new CornerRadius(8)
        };
        loginButton.Click += async (s, e) => await ViewModel.LoginCommand.ExecuteAsync(null);

        var cardStack = new StackPanel
        {
            Width = 440,
            Spacing = 16,
            Padding = new Thickness(36),
            Background = new SolidColorBrush(StitchTokens.SurfaceWhite),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(StitchTokens.SurfaceBorder),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Đăng nhập", FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(StitchTokens.ContentTitle) },
                new TextBlock { Text = "Chào mừng quay lại, vui lòng đăng nhập để tiếp tục", FontSize = 13, Foreground = new SolidColorBrush(StitchTokens.ContentMuted) },
                errorBanner,
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = "Tên đăng nhập", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Foreground = new SolidColorBrush(StitchTokens.ContentBody) },
                        usernameBox,
                        usernameErrorText
                    }
                },
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = "Mật khẩu", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Foreground = new SolidColorBrush(StitchTokens.ContentBody) },
                        pwdGrid,
                        passwordErrorText
                    }
                },
                rememberCheckBox,
                loginButton,
                new TextBlock
                {
                    Text = "TechStore Management System",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(StitchTokens.ContentMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 0)
                }
            }
        };

        var outerContainer = new Grid
        {
            Background = new SolidColorBrush(StitchTokens.SurfaceCanvas)
        };
        outerContainer.Children.Add(cardStack);
        return outerContainer;
    }
}
