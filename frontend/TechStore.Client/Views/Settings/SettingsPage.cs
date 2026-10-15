using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Settings;
using TechStore.Client.Views.Shell;
using Windows.UI;

namespace TechStore.Client.Views.Settings;

public sealed class SettingsPage : Page, IRoutePage
{
    public SettingsViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    private readonly Border _saveSuccessBanner;
    private readonly TextBlock _saveErrorText;
    private readonly Button _saveButton;

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        Background = new SolidColorBrush(StitchTokens.SurfaceCanvas);

        // Header Section
        var breadcrumb = new TextBlock
        {
            Text = "Cấu hình hệ thống > Cài đặt chung",
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ContentMuted)
        };

        var titleText = new TextBlock
        {
            Text = "Cài đặt",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(StitchTokens.ContentTitle)
        };

        var buildVersion = new TextBlock
        {
            Text = $"v2.4.0 · {ViewModel.RuntimeDescription}",
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ContentMuted),
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var titleRowGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        Grid.SetColumn(titleText, 0);
        Grid.SetColumn(buildVersion, 1);
        titleRowGrid.Children.Add(titleText);
        titleRowGrid.Children.Add(buildVersion);

        var headerStack = new StackPanel
        {
            Spacing = 4,
            Children = { breadcrumb, titleRowGrid }
        };

        // Success Notification Banner (Stitch #ECFDF5 / #A7F3D0 / #065F46)
        _saveSuccessBanner = new Border
        {
            Background = new SolidColorBrush(StitchTokens.SuccessBg),
            BorderBrush = new SolidColorBrush(StitchTokens.SuccessBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Visibility = Visibility.Collapsed,
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE73E", Foreground = new SolidColorBrush(StitchTokens.SuccessGreen), FontSize = 16 },
                            new TextBlock
                            {
                                Text = "Đã lưu thay đổi thông tin cửa hàng thành công.",
                                FontSize = 13,
                                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                                Foreground = new SolidColorBrush(StitchTokens.SuccessText)
                            }
                        }
                    }
                }
            }
        };

        _saveErrorText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(StitchTokens.ErrorRed),
            Visibility = Visibility.Collapsed
        };

        // Form Fields (Stitch c_i_t_tab_1_c_a_h_ng_l_u_thay_i max-w 680px)
        var storeNameBox = new TextBox
        {
            Text = ViewModel.StoreName,
            PlaceholderText = "Nhập tên cửa hàng"
        };
        storeNameBox.TextChanged += (s, e) => ViewModel.StoreName = storeNameBox.Text;

        var storeAddressBox = new TextBox
        {
            Text = ViewModel.StoreAddress,
            PlaceholderText = "Địa chỉ trụ sở kinh doanh chính"
        };
        storeAddressBox.TextChanged += (s, e) => ViewModel.StoreAddress = storeAddressBox.Text;

        var storePhoneBox = new TextBox
        {
            Text = ViewModel.StorePhone,
            PlaceholderText = "Số điện thoại hotline"
        };
        storePhoneBox.TextChanged += (s, e) => ViewModel.StorePhone = storePhoneBox.Text;

        var storeEmailBox = new TextBox
        {
            Text = ViewModel.StoreEmail,
            PlaceholderText = "Email liên hệ chính thức"
        };
        storeEmailBox.TextChanged += (s, e) => ViewModel.StoreEmail = storeEmailBox.Text;

        var storeTaxIdBox = new TextBox
        {
            Text = ViewModel.StoreTaxId,
            PlaceholderText = "Mã số thuế doanh nghiệp"
        };
        storeTaxIdBox.TextChanged += (s, e) => ViewModel.StoreTaxId = storeTaxIdBox.Text;

        // Action Save Button
        _saveButton = new Button
        {
            Content = "Lưu thay đổi",
            Background = new SolidColorBrush(StitchTokens.Primary),
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Padding = new Thickness(20, 8, 20, 8),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _saveButton.Click += async (s, e) => await ViewModel.SaveStoreSettingsCommand.ExecuteAsync(null);

        // CommunityToolkit SettingsExpander & SettingsCard
        var storeSection = new SettingsExpander
        {
            Header = "Thông tin cửa hàng",
            Description = "Quản lý thông tin hiển thị trên hóa đơn và chứng từ bán lẻ TechStore",
            IsExpanded = true,
            Items =
            {
                new SettingsCard { Header = "Tên cửa hàng *", Description = "In trên tiêu đề hóa đơn bán hàng", Content = storeNameBox },
                new SettingsCard { Header = "Địa chỉ trụ sở *", Description = "Địa chỉ giao dịch kinh doanh", Content = storeAddressBox },
                new SettingsCard { Header = "Số hotline *", Description = "Số tổng đài chăm sóc khách hàng", Content = storePhoneBox },
                new SettingsCard { Header = "Email liên hệ", Description = "Hộp thư tiếp nhận phản hồi", Content = storeEmailBox },
                new SettingsCard { Header = "Mã số thuế", Description = "Mã số thuế đăng ký kinh doanh", Content = storeTaxIdBox },
                new SettingsCard { Content = _saveButton, HorizontalContentAlignment = HorizontalAlignment.Right }
            }
        };

        // Theme preference card
        var themeComboBox = new ComboBox
        {
            ItemsSource = ViewModel.ThemeOptions,
            SelectedIndex = ViewModel.SelectedThemeIndex,
            MinWidth = 180
        };
        themeComboBox.SelectionChanged += (s, e) => ViewModel.SelectedThemeIndex = themeComboBox.SelectedIndex;

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
                new SettingsCard { Header = "Ứng dụng", Description = ViewModel.ApplicationName },
                new SettingsCard { Header = "Phiên bản", Description = ViewModel.ApplicationVersion },
                new SettingsCard { Header = "Nền tảng", Description = ViewModel.RuntimeDescription }
            }
        };

        var formContentStack = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                _saveSuccessBanner,
                _saveErrorText,
                storeSection,
                themeCard,
                appInfoExpander
            }
        };

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = formContentStack
        };

        var rootGrid = new Grid
        {
            Padding = new Thickness(24),
            RowSpacing = 20,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };
        Grid.SetRow(headerStack, 0);
        Grid.SetRow(scrollViewer, 1);
        rootGrid.Children.Add(headerStack);
        rootGrid.Children.Add(scrollViewer);

        Content = rootGrid;

        // View model bindings
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.ShowSaveSuccess))
            {
                _saveSuccessBanner.Visibility = ViewModel.ShowSaveSuccess ? Visibility.Visible : Visibility.Collapsed;
            }
            if (e.PropertyName == nameof(ViewModel.SaveErrorMessage))
            {
                _saveErrorText.Text = ViewModel.SaveErrorMessage;
                _saveErrorText.Visibility = string.IsNullOrEmpty(ViewModel.SaveErrorMessage) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (e.PropertyName == nameof(ViewModel.IsSaving))
            {
                _saveButton.IsEnabled = !ViewModel.IsSaving;
                _saveButton.Content = ViewModel.IsSaving ? "Đang lưu..." : "Lưu thay đổi";
            }
            if (e.PropertyName == nameof(ViewModel.StoreName))
            {
                if (storeNameBox.Text != ViewModel.StoreName) storeNameBox.Text = ViewModel.StoreName;
            }
            if (e.PropertyName == nameof(ViewModel.StoreAddress))
            {
                if (storeAddressBox.Text != ViewModel.StoreAddress) storeAddressBox.Text = ViewModel.StoreAddress;
            }
            if (e.PropertyName == nameof(ViewModel.StorePhone))
            {
                if (storePhoneBox.Text != ViewModel.StorePhone) storePhoneBox.Text = ViewModel.StorePhone;
            }
            if (e.PropertyName == nameof(ViewModel.StoreEmail))
            {
                if (storeEmailBox.Text != ViewModel.StoreEmail) storeEmailBox.Text = ViewModel.StoreEmail;
            }
            if (e.PropertyName == nameof(ViewModel.StoreTaxId))
            {
                if (storeTaxIdBox.Text != ViewModel.StoreTaxId) storeTaxIdBox.Text = ViewModel.StoreTaxId;
            }
        };
    }
}
