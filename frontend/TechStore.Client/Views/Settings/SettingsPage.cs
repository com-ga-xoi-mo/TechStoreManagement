using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Settings;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using Windows.UI;

namespace TechStore.Client.Views.Settings;

public sealed class SettingsPage : Page, IRoutePage
{
    public SettingsViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        this.Background(new SolidColorBrush(StitchTokens.SurfaceCanvas))
            .Content(
                new Grid()
                    .Padding(24)
                    .RowSpacing(20)
                    .RowDefinitions(
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(new GridLength(1, GridUnitType.Star))
                    )
                    .Children(
                        BuildHeader().Grid(row: 0),
                        new ScrollViewer()
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .Content(BuildFormContent())
                            .Grid(row: 1)
                    )
            );
    }

    private UIElement BuildHeader()
    {
        return new StackPanel()
            .Spacing(4)
            .Children(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto)
                    )
                    .Children(
                        new StackPanel()
                            .Spacing(4)
                            .Grid(column: 0)
                            .Children(
                                new TextBlock()
                                    .Text("Cài đặt")
                                    .FontSize(24)
                                    .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                                    .Foreground(new SolidColorBrush(StitchTokens.ContentTitle)),
                                new TextBlock()
                                    .Text("Quản lý thông tin cửa hàng và giao diện ứng dụng.")
                                    .FontSize(13)
                                    .Foreground(new SolidColorBrush(StitchTokens.ContentMuted))),
                        new TextBlock()
                            .Text($"v2.4.0 · {ViewModel.RuntimeDescription}")
                            .FontSize(12)
                            .Foreground(new SolidColorBrush(StitchTokens.ContentMuted))
                            .VerticalAlignment(VerticalAlignment.Bottom)
                            .Grid(column: 1)
                    )
            );
    }

    private UIElement BuildFormContent()
    {
        // Success Notification Banner
        var saveSuccessBanner = new Border()
            .Background(new SolidColorBrush(StitchTokens.SuccessBg))
            .BorderBrush(new SolidColorBrush(StitchTokens.SuccessBorder))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(16, 12, 16, 12))
            .Visibility(x => x.Binding(() => ViewModel.ShowSaveSuccess).Convert((bool show) => show ? Visibility.Visible : Visibility.Collapsed))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(8)
                    .Children(
                        new FontIcon()
                            .Glyph("\uE73E")
                            .Foreground(new SolidColorBrush(StitchTokens.SuccessGreen))
                            .FontSize(16),
                        new TextBlock()
                            .Text("Đã lưu thay đổi thông tin cửa hàng thành công.")
                            .FontSize(13)
                            .FontWeight(Microsoft.UI.Text.FontWeights.Medium)
                            .Foreground(new SolidColorBrush(StitchTokens.SuccessText))
                    )
            );

        var saveErrorText = new TextBlock()
            .FontSize(12)
            .Foreground(new SolidColorBrush(StitchTokens.ErrorRed))
            .Text(x => x.Binding(() => ViewModel.SaveErrorMessage))
            .Visibility(x => x.Binding(() => ViewModel.SaveErrorMessage).Convert((string msg) => string.IsNullOrEmpty(msg) ? Visibility.Collapsed : Visibility.Visible));

        // Form Fields
        var storeNameBox = new TextBox()
            .PlaceholderText("Nhập tên cửa hàng")
            .Text(x => x.Binding(() => ViewModel.StoreName).Mode(BindingMode.TwoWay));

        var storeAddressBox = new TextBox()
            .PlaceholderText("Địa chỉ trụ sở kinh doanh chính")
            .Text(x => x.Binding(() => ViewModel.StoreAddress).Mode(BindingMode.TwoWay));

        var storePhoneBox = new TextBox()
            .PlaceholderText("Số điện thoại hotline")
            .Text(x => x.Binding(() => ViewModel.StorePhone).Mode(BindingMode.TwoWay));

        var storeEmailBox = new TextBox()
            .PlaceholderText("Email liên hệ chính thức")
            .Text(x => x.Binding(() => ViewModel.StoreEmail).Mode(BindingMode.TwoWay));

        var storeTaxIdBox = new TextBox()
            .PlaceholderText("Mã số thuế doanh nghiệp")
            .Text(x => x.Binding(() => ViewModel.StoreTaxId).Mode(BindingMode.TwoWay));

        var saveButton = new Button()
            .Background(new SolidColorBrush(StitchTokens.Primary))
            .Foreground(new SolidColorBrush(Colors.White))
            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
            .Padding(new Thickness(20, 8, 20, 8))
            .CornerRadius(new CornerRadius(6))
            .HorizontalAlignment(HorizontalAlignment.Right)
            .Command(x => x.Binding(() => ViewModel.SaveStoreSettingsCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.IsSaving).Convert((bool saving) => !saving))
            .Content(new TextBlock()
                .Foreground(new SolidColorBrush(Colors.White))
                .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                .Text(x => x.Binding(() => ViewModel.IsSaving).Convert((bool saving) => saving ? "Đang lưu..." : "Lưu thay đổi")));

        // CommunityToolkit SettingsExpander & SettingsCard
        var expanderItemPadding = new Thickness(16, 8, 16, 8);
        var storeSection = new SettingsExpander
        {
            Header = "Thông tin cửa hàng",
            Description = "Quản lý thông tin hiển thị trên hóa đơn và chứng từ bán lẻ TechStore",
            IsExpanded = true,
            Items =
            {
                new SettingsCard { Header = "Tên cửa hàng *", Description = "In trên tiêu đề hóa đơn bán hàng", Content = storeNameBox, Padding = expanderItemPadding },
                new SettingsCard { Header = "Địa chỉ trụ sở *", Description = "Địa chỉ giao dịch kinh doanh", Content = storeAddressBox, Padding = expanderItemPadding },
                new SettingsCard { Header = "Số hotline *", Description = "Số tổng đài chăm sóc khách hàng", Content = storePhoneBox, Padding = expanderItemPadding },
                new SettingsCard { Header = "Email liên hệ", Description = "Hộp thư tiếp nhận phản hồi", Content = storeEmailBox, Padding = expanderItemPadding },
                new SettingsCard { Header = "Mã số thuế", Description = "Mã số thuế đăng ký kinh doanh", Content = storeTaxIdBox, Padding = expanderItemPadding },
                new SettingsCard { Content = saveButton, HorizontalContentAlignment = HorizontalAlignment.Right, Padding = expanderItemPadding }
            }
        };

        // Theme preference card
        var themeComboBox = new ComboBox()
            .ItemsSource(ViewModel.ThemeOptions)
            .MinWidth(180)
            .SelectedIndex(x => x.Binding(() => ViewModel.SelectedThemeIndex).Mode(BindingMode.TwoWay));

        var themeCard = new SettingsCard
        {
            Header = "Giao diện hệ thống",
            Description = "Áp dụng ngay cho cửa sổ TechStore trong phiên hiện tại.",
            Content = themeComboBox
        };

        var appInfoExpander = new SettingsExpander
        {
            Header = "Thông tin ứng dụng",
            Description = "Bản dựng và thông số kỹ thuật client",
            IsExpanded = false,
            Items =
            {
                new SettingsCard { Header = "Ứng dụng", Description = ViewModel.ApplicationName, Padding = expanderItemPadding },
                new SettingsCard { Header = "Phiên bản", Description = ViewModel.ApplicationVersion, Padding = expanderItemPadding },
                new SettingsCard { Header = "Nền tảng", Description = ViewModel.RuntimeDescription, Padding = expanderItemPadding }
            }
        };

        return new StackPanel()
            .Spacing(16)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Children(
                saveSuccessBanner,
                saveErrorText,
                storeSection,
                themeCard,
                appInfoExpander
            );
    }
}
