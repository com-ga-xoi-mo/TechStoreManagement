using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Identity;
using Uno.Extensions.Markup;
using Windows.UI;

namespace TechStore.Client.Views.Identity;

public sealed class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage(LoginViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        this.Background(new SolidColorBrush(StitchTokens.SurfaceCanvas))
            .Content(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(480, GridUnitType.Pixel)),
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star))
                    )
                    .Children(
                        BuildBrandingPanel().Grid(column: 0),
                        BuildFormPanel().Grid(column: 1)
                    )
            );
    }

    private static UIElement BuildBrandingPanel()
    {
        return new Border()
            .Background(new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x0B, 0x15, 0x28), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x0F, 0x1E, 0x3D), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x0A, 0x25, 0x58), Offset = 1 }
                }
            })
            .Padding(48)
            .Child(
                new Grid()
                    .RowDefinitions(
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(new GridLength(1, GridUnitType.Star)),
                        new RowDefinition().Height(GridLength.Auto)
                    )
                    .Children(
                        // Header logo
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(14)
                            .Children(
                                new Border()
                                    .Width(44)
                                    .Height(44)
                                    .CornerRadius(new CornerRadius(12))
                                    .Background(new SolidColorBrush(StitchTokens.Primary))
                                    .Child(
                                        new FontIcon()
                                            .Glyph("\uE7F8")
                                            .Foreground(new SolidColorBrush(Colors.White))
                                            .FontSize(22)
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                    ),
                                new StackPanel()
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Children(
                                        new TextBlock()
                                            .Text("TechStore")
                                            .FontSize(22)
                                            .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                                            .Foreground(new SolidColorBrush(Colors.White)),
                                        new TextBlock()
                                            .Text("Hệ thống quản lý bán lẻ")
                                            .FontSize(12)
                                            .Foreground(new SolidColorBrush(Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF)))
                                    )
                            )
                            .Grid(row: 0),

                        // Center: Highlights
                        new StackPanel()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Spacing(20)
                            .Children(
                                new Border()
                                    .HorizontalAlignment(HorizontalAlignment.Left)
                                    .Background(new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x66, 0xFF)))
                                    .BorderBrush(new SolidColorBrush(Color.FromArgb(0x66, 0x60, 0xA5, 0xFA)))
                                    .BorderThickness(new Thickness(1))
                                    .CornerRadius(new CornerRadius(20))
                                    .Padding(new Thickness(12, 6, 12, 6))
                                    .Child(
                                        new TextBlock()
                                            .Text("● Nền tảng vận hành POS & Kho đa kênh")
                                            .FontSize(12)
                                            .FontWeight(Microsoft.UI.Text.FontWeights.Medium)
                                            .Foreground(new SolidColorBrush(Color.FromArgb(0xFF, 0x93, 0xC5, 0xFD)))
                                    ),
                                new TextBlock()
                                    .Text("Quản lý bán lẻ thiết bị công nghệ")
                                    .FontSize(26)
                                    .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                                    .Foreground(new SolidColorBrush(Colors.White))
                                    .TextWrapping(TextWrapping.Wrap),
                                new TextBlock()
                                    .Text("Hệ thống tích hợp theo dõi tồn kho Serial/IMEI, quản lý bán hàng tại quầy và chăm sóc khách hàng chuyên nghiệp.")
                                    .FontSize(13)
                                    .Foreground(new SolidColorBrush(Color.FromArgb(0xCC, 0xCB, 0xD5, 0xE1)))
                                    .TextWrapping(TextWrapping.Wrap),
                                new Border()
                                    .Background(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)))
                                    .BorderBrush(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)))
                                    .BorderThickness(new Thickness(1))
                                    .CornerRadius(new CornerRadius(16))
                                    .Padding(20)
                                    .Child(
                                        new StackPanel()
                                            .Spacing(14)
                                            .Children(
                                                new StackPanel()
                                                    .Orientation(Orientation.Horizontal)
                                                    .Spacing(12)
                                                    .Children(
                                                        new FontIcon()
                                                            .Glyph("\uE890")
                                                            .Foreground(new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0xA5, 0xFA)))
                                                            .FontSize(16),
                                                        new TextBlock()
                                                            .Text("Kiểm soát 15 số IMEI chính xác theo từng máy")
                                                            .FontSize(13)
                                                            .Foreground(new SolidColorBrush(Colors.White))
                                                    ),
                                                new StackPanel()
                                                    .Orientation(Orientation.Horizontal)
                                                    .Spacing(12)
                                                    .Children(
                                                        new FontIcon()
                                                            .Glyph("\uE8C7")
                                                            .Foreground(new SolidColorBrush(Color.FromArgb(0xFF, 0x34, 0xD3, 0x99)))
                                                            .FontSize(16),
                                                        new TextBlock()
                                                            .Text("Thanh toán nhanh: Tiền mặt, Thẻ POS & VietQR")
                                                            .FontSize(13)
                                                            .Foreground(new SolidColorBrush(Colors.White))
                                                    )
                                            )
                                    )
                            )
                            .Grid(row: 1),

                        // Footer version
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                new TextBlock()
                                    .Text("Phiên bản Desktop v2.4 · Bảo mật xác thực doanh nghiệp")
                                    .FontSize(12)
                                    .Foreground(new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)))
                            )
                            .Grid(row: 2)
                    )
            );
    }

    private UIElement BuildFormPanel()
    {
        var passwordBox = new PasswordBox
        {
            PlaceholderText = "Nhập mật khẩu của bạn",
            Height = 40,
            CornerRadius = new CornerRadius(8)
        };
        passwordBox.PasswordChanged += (s, e) =>
        {
            if (!ViewModel.IsPasswordVisible)
            {
                ViewModel.Password = passwordBox.Password;
            }
        };

        var passwordRevealBox = new TextBox()
            .PlaceholderText("Nhập mật khẩu của bạn")
            .Height(40)
            .CornerRadius(new CornerRadius(8))
            .Text(x => x.Binding(() => ViewModel.Password).Mode(BindingMode.TwoWay))
            .Visibility(x => x.Binding(() => ViewModel.IsPasswordVisible).Convert((bool visible) => visible ? Visibility.Visible : Visibility.Collapsed));

        passwordBox.Visibility = ViewModel.IsPasswordVisible ? Visibility.Collapsed : Visibility.Visible;

        var pwdGrid = new Grid()
            .Children(
                passwordBox,
                passwordRevealBox,
                new Button()
                    .Content(new FontIcon().Glyph("\uE7B3").FontSize(14))
                    .Background(new SolidColorBrush(Colors.Transparent))
                    .BorderThickness(new Thickness(0))
                    .Padding(8)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Command(x => x.Binding(() => ViewModel.TogglePasswordVisibilityCommand))
            );

        // Keep passwordBox in sync when toggling visibility
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsPasswordVisible))
            {
                if (ViewModel.IsPasswordVisible)
                {
                    passwordBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    passwordBox.Password = ViewModel.Password;
                    passwordBox.Visibility = Visibility.Visible;
                }
            }
        };

        var errorBanner = new Border()
            .Background(new SolidColorBrush(StitchTokens.ErrorBg))
            .BorderBrush(new SolidColorBrush(StitchTokens.ErrorBorder))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12, 10, 12, 10))
            .Visibility(x => x.Binding(() => ViewModel.HasError).Convert((bool hasErr) => hasErr ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(8)
                    .Children(
                        new FontIcon()
                            .Glyph("\uE783")
                            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed))
                            .FontSize(16),
                        new TextBlock()
                            .FontSize(13)
                            .Foreground(new SolidColorBrush(StitchTokens.ErrorText))
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(x => x.Binding(() => ViewModel.ErrorMessage))
                    )
            );

        var loginButton = new Button()
            .Height(44)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Background(new SolidColorBrush(StitchTokens.Primary))
            .CornerRadius(new CornerRadius(8))
            .IsEnabled(x => x.Binding(() => ViewModel.IsLoggingIn).Convert((bool loading) => !loading))
            .Command(x => x.Binding(() => ViewModel.LoginCommand))
            .Content(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(8)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new ProgressRing()
                            .Width(16)
                            .Height(16)
                            .Foreground(new SolidColorBrush(Colors.White))
                            .IsActive(x => x.Binding(() => ViewModel.IsLoggingIn))
                            .Visibility(x => x.Binding(() => ViewModel.IsLoggingIn).Convert((bool loading) => loading ? Visibility.Visible : Visibility.Collapsed)),
                        new TextBlock()
                            .FontSize(14)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .Foreground(new SolidColorBrush(Colors.White))
                            .Text(x => x.Binding(() => ViewModel.IsLoggingIn).Convert((bool loading) => loading ? "Đang xử lý..." : "Đăng nhập"))
                    )
            );

        var cardStack = new StackPanel()
            .Width(440)
            .Spacing(16)
            .Padding(36)
            .Background(new SolidColorBrush(StitchTokens.SurfaceWhite))
            .CornerRadius(new CornerRadius(16))
            .BorderBrush(new SolidColorBrush(StitchTokens.SurfaceBorder))
            .BorderThickness(new Thickness(1))
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                new TextBlock()
                    .Text("Đăng nhập")
                    .FontSize(26)
                    .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                    .Foreground(new SolidColorBrush(StitchTokens.ContentTitle)),
                new TextBlock()
                    .Text("Chào mừng quay lại, vui lòng đăng nhập để tiếp tục")
                    .FontSize(13)
                    .Foreground(new SolidColorBrush(StitchTokens.ContentMuted)),
                errorBanner,
                new StackPanel()
                    .Spacing(6)
                    .Children(
                        new TextBlock()
                            .Text("Tên đăng nhập")
                            .FontSize(13)
                            .FontWeight(Microsoft.UI.Text.FontWeights.Medium)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentBody)),
                        new TextBox()
                            .PlaceholderText("Nhập tên đăng nhập hoặc mã nhân viên")
                            .Height(40)
                            .CornerRadius(new CornerRadius(8))
                            .Text(x => x.Binding(() => ViewModel.Username).Mode(BindingMode.TwoWay)),
                        new TextBlock()
                            .FontSize(12)
                            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed))
                            .Text(x => x.Binding(() => ViewModel.UsernameError))
                            .Visibility(x => x.Binding(() => ViewModel.UsernameError).Convert((string err) => string.IsNullOrEmpty(err) ? Visibility.Collapsed : Visibility.Visible))
                    ),
                new StackPanel()
                    .Spacing(6)
                    .Children(
                        new TextBlock()
                            .Text("Mật khẩu")
                            .FontSize(13)
                            .FontWeight(Microsoft.UI.Text.FontWeights.Medium)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentBody)),
                        pwdGrid,
                        new TextBlock()
                            .FontSize(12)
                            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed))
                            .Text(x => x.Binding(() => ViewModel.PasswordError))
                            .Visibility(x => x.Binding(() => ViewModel.PasswordError).Convert((string err) => string.IsNullOrEmpty(err) ? Visibility.Collapsed : Visibility.Visible))
                    ),
                new CheckBox()
                    .Content("Ghi nhớ đăng nhập")
                    .IsChecked(x => x.Binding(() => ViewModel.RememberMe).Mode(BindingMode.TwoWay)),
                loginButton,
                new TextBlock()
                    .Text("TechStore Management System")
                    .FontSize(12)
                    .Foreground(new SolidColorBrush(StitchTokens.ContentMuted))
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 12, 0, 0))
            );

        return new Grid()
            .Background(new SolidColorBrush(StitchTokens.SurfaceCanvas))
            .Children(cardStack);
    }
}
