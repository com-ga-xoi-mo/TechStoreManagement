using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Models.Customers;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Customers;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Customers;

public sealed class CustomerFormDialogView : UserControl
{
    private const string Surface = AppColors.Surface;
    private const string Subtle = AppColors.Subtle;
    private const string Text = AppColors.Text;
    private const string SecondaryText = AppColors.SecondaryText;
    private const string Stroke = AppColors.Stroke;
    private const string InputStroke = AppColors.InputStroke;
    private const string Primary = AppColors.Primary;
    private const string OnPrimary = AppColors.OnPrimary;
    private const string InfoFill = AppColors.InfoFill;
    private const string InfoText = AppColors.InfoText;
    private const string DangerText = AppColors.DangerText;

    public CustomerFormViewModel ViewModel { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public Control InitialFocusTarget { get; }

    public CustomerFormDialogView(
        CustomerFormViewModel viewModel,
        IAsyncRelayCommand saveCommand,
        IRelayCommand closeCommand)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        SaveCommand = saveCommand ?? throw new ArgumentNullException(nameof(saveCommand));
        CloseCommand = closeCommand ?? throw new ArgumentNullException(nameof(closeCommand));
        DataContext = ViewModel;

        var fullName = FieldTextBox("Nhập đầy đủ họ tên khách hàng...")
            .MaxLength(60)
            .Text(x => x.Binding(() => ViewModel.FullName).Mode(BindingMode.TwoWay));
        InitialFocusTarget = fullName;
        var phone = FieldTextBox("09xx xxx xxx", 76)
            .MaxLength(10)
            .Text(x => x.Binding(() => ViewModel.Phone).Mode(BindingMode.TwoWay));
        var email = FieldTextBox("tenkhachhang@domain.vn")
            .Text(x => x.Binding(() => ViewModel.Email).Mode(BindingMode.TwoWay));
        var dateOfBirth = FieldTextBox("dd/mm/yyyy", leftPadding: 44)
            .MaxLength(10)
            .Text(x => x.Binding(() => ViewModel.DateOfBirth).Mode(BindingMode.TwoWay));
        var datePickerFlyout = new DatePickerFlyout()
            .Date(x => x.Binding(() => ViewModel.DateOfBirthDate).Mode(BindingMode.TwoWay));
        var datePickerButton = new Button()
            .Width(40).Height(38).Padding(0)
            .Background(ThemeResource.Get<Brush>(Surface))
            .BorderThickness(0)
            .Content(Icon("\uE787", 17, Text))
            .Flyout(datePickerFlyout);
        AutomationProperties.SetName(datePickerButton, "Chọn ngày sinh trên lịch");
        var address = FieldTextBox("Nhập địa chỉ hoặc tỉnh/thành phố")
            .Text(x => x.Binding(() => ViewModel.Address).Mode(BindingMode.TwoWay));

        var form = new StackPanel()
            .Spacing(16)
            .Children(
                Field("Họ và tên", true, "Tối đa 60 ký tự", "\uE77B", fullName,
                    Error(
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.FullNameError))
                            .Visibility(x => x.Binding(() => ViewModel.FullNameError)
                                .Convert((string message) => string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible)))),
                PhoneField(phone),
                Field("Địa chỉ Email", false, null, "\uE715", email,
                    Error(
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.EmailError))
                            .Visibility(x => x.Binding(() => ViewModel.EmailError)
                                .Convert((string message) => string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible))),
                    "Nhận hóa đơn điện tử, thông báo bảo hành và thông tin bàn giao thiết bị."),
                DateOfBirthField(dateOfBirth, datePickerButton,
                    Error(
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.DateOfBirthError))
                            .Visibility(x => x.Binding(() => ViewModel.DateOfBirthError)
                                .Convert((string message) => string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible)))),
                Field("Địa chỉ / Tỉnh, thành phố", false, null, "\uE707", address,
                    Error(
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.AddressError))
                            .Visibility(x => x.Binding(() => ViewModel.AddressError)
                                .Convert((string message) => string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible)))),
                InformationBanner(),
                Error(
                    new TextBlock()
                        .Text(x => x.Binding(() => ViewModel.FormError))
                        .Visibility(x => x.Binding(() => ViewModel.FormError)
                            .Convert((string message) => string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible))));

        var closeButton = new Button()
            .Width(34).Height(34).Padding(0)
            .CornerRadius(new CornerRadius(8))
            .Background(ThemeResource.Get<Brush>(Surface))
            .Content(Icon("\uE711", 16, SecondaryText))
            .Command(CloseCommand);
        AutomationProperties.SetName(closeButton, "Đóng form khách hàng");
        closeButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape });

        var cancelButton = new Button()
            .Style(StaticResource.Get<Style>("TechStoreButtonStyle"))
            .MinWidth(92)
            .Content("Hủy bỏ")
            .Command(CloseCommand);
        AutomationProperties.SetName(cancelButton, "Hủy thay đổi khách hàng");

        var saveButton = new Button()
            .Style(StaticResource.Get<Style>("TechStorePrimaryButtonStyle"))
            .MinWidth(146)
            .IsEnabled(x => x.Binding(() => ViewModel.IsSaving).Convert((bool saving) => !saving))
            .Content(new StackPanel()
                .Orientation(Orientation.Horizontal).Spacing(8)
                .Children(
                    Icon("\uE73E", 16, OnPrimary),
                    new TextBlock().VerticalAlignment(VerticalAlignment.Center)
                        .Text(x => x.Binding(() => ViewModel.SaveButtonText))))
            .Command(SaveCommand);
        AutomationProperties.SetName(saveButton, "Lưu thông tin khách hàng");

        Content = new Border()
            .Width(540).MaxHeight(760)
            .Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke))
            .BorderThickness(1)
            .CornerRadius(new CornerRadius(12))
            .Child(new Grid()
                .RowDefinitions(
                    new RowDefinition().Height(GridLength.Auto),
                    new RowDefinition().Height(GridLength.Auto),
                    new RowDefinition().Height(new GridLength(1, GridUnitType.Star)),
                    new RowDefinition().Height(GridLength.Auto))
                .Children(
                    Header(closeButton).Grid(row: 0),
                    MembershipSummary().Grid(row: 1),
                    new ScrollViewer()
                        .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                        .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                        .Padding(new Thickness(24, 20, 24, 20))
                        .Content(form).Grid(row: 2),
                    new Border()
                        .Background(ThemeResource.Get<Brush>(Subtle))
                        .BorderBrush(ThemeResource.Get<Brush>(Stroke))
                        .BorderThickness(new Thickness(0, 1, 0, 0))
                        .Padding(new Thickness(24, 14, 24, 14))
                        .Child(new StackPanel()
                            .Orientation(Orientation.Horizontal).Spacing(12)
                            .HorizontalAlignment(HorizontalAlignment.Right)
                            .Children(cancelButton, saveButton))
                        .Grid(row: 3)));
    }

    private UIElement Header(Button closeButton) =>
        new Grid()
            .Padding(new Thickness(24, 20, 20, 18)).ColumnSpacing(14)
            .ColumnDefinitions(
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                new Border().Width(42).Height(42).CornerRadius(new CornerRadius(10))
                    .Background(ThemeResource.Get<Brush>(InfoFill))
                    .Child(Icon("\uE77B", 21, InfoText)).Grid(column: 0),
                new StackPanel().Spacing(2).VerticalAlignment(VerticalAlignment.Center)
                    .Children(
                        new TextBlock().Style(StaticResource.Get<Style>("TechStoreHeadingTextStyle"))
                            .Text(x => x.Binding(() => ViewModel.Title)),
                        new TextBlock().Style(StaticResource.Get<Style>("TechStoreBodyTextStyle"))
                            .Foreground(ThemeResource.Get<Brush>(SecondaryText))
                            .Text(x => x.Binding(() => ViewModel.Subtitle)))
                    .Grid(column: 1),
                closeButton.Grid(column: 2));

    private UIElement MembershipSummary() =>
        new Border()
            .Background(ThemeResource.Get<Brush>(Subtle))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke))
            .BorderThickness(new Thickness(0, 1, 0, 1))
            .Padding(new Thickness(24, 10, 24, 10))
            .Visibility(x => x.Binding(() => ViewModel.ShowMembershipSummary)
                .Convert((bool visible) => visible ? Visibility.Visible : Visibility.Collapsed))
            .Child(new Grid()
                .ColumnDefinitions(
                    new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition().Width(GridLength.Auto))
                .Children(
                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(8)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Children(Icon("\uE83D", 16, SecondaryText), Caption("Thẻ hội viên TechStore VIP:"))
                        .Grid(column: 0),
                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(8)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Children(
                            BuildTierBadge(),
                            new TextBlock().FontWeight(FontWeights.SemiBold)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .Text(x => x.Binding(() => ViewModel.LoyaltyPointsLabel)),
                            Badge("Chỉ đọc", Subtle, SecondaryText))
                        .Grid(column: 1)));

    private Grid BuildTierBadge()
    {
        var variants = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var tier in CustomerTierPresentation.All)
        {
            variants.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(9, 3, 9, 3),
                Child = new TextBlock
                {
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                }
                    .Foreground(ThemeResource.Get<Brush>(tier.TextResourceKey))
                    .Text(x => x.Binding(() => ViewModel.TierLabel))
            }
                .Background(ThemeResource.Get<Brush>(tier.FillResourceKey))
                .Visibility(x => x.Binding(() => ViewModel.TierCode)
                    .Convert((string value) => value == tier.Tier ? Visibility.Visible : Visibility.Collapsed)));
        }

        return variants;
    }

    private UIElement PhoneField(TextBox phone)
    {
        var input = InputWithIcon("\uE717", phone);
        input.Children.Add(new Border()
            .Background(ThemeResource.Get<Brush>(Subtle))
            .CornerRadius(new CornerRadius(4)).Padding(new Thickness(7, 3, 7, 3))
            .HorizontalAlignment(HorizontalAlignment.Right).VerticalAlignment(VerticalAlignment.Center)
            .Margin(new Thickness(0, 0, 10, 0)).IsHitTestVisible(false)
            .Child(Caption("VN +84")));
        return new StackPanel().Spacing(6)
            .Children(
                FieldLabel("Số điện thoại", true, "Đã xác thực OTP"),
                input,
                Error(
                    new TextBlock()
                        .Text(x => x.Binding(() => ViewModel.PhoneError))
                        .Visibility(x => x.Binding(() => ViewModel.PhoneError)
                            .Convert((string message) => string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible))),
                Supporting("\uE946", "Dùng để tra cứu lịch sử mua hàng, bảo hành điện tử và tích điểm thành viên."));
    }

    private static UIElement Field(string label, bool required, string? hint, string glyph,
        TextBox input, TextBlock error, string? supporting = null)
    {
        var panel = new StackPanel().Spacing(6)
            .Children(FieldLabel(label, required, hint), InputWithIcon(glyph, input), error);
        if (!string.IsNullOrWhiteSpace(supporting)) panel.Children.Add(Supporting("\uE8A5", supporting));
        return panel;
    }

    private static UIElement DateOfBirthField(TextBox input, Button datePickerButton, TextBlock error) =>
        new StackPanel().Spacing(6)
            .Children(
                FieldLabel("Ngày sinh", false, "dd/mm/yyyy"),
                new Grid().Children(
                    input,
                    datePickerButton.HorizontalAlignment(HorizontalAlignment.Left)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Margin(new Thickness(1, 0, 0, 0))),
                error);

    private static Grid FieldLabel(string label, bool required, string? hint)
    {
        var title = new StackPanel().Orientation(Orientation.Horizontal).Spacing(3)
            .Children(new TextBlock().FontSize(13).FontWeight(FontWeights.SemiBold).Text(label));
        if (required)
            title.Children.Add(new TextBlock().FontSize(13).FontWeight(FontWeights.Bold).Text("*")
                .Foreground(ThemeResource.Get<Brush>(DangerText)));
        var row = new Grid()
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(title.Grid(column: 0));
        if (!string.IsNullOrWhiteSpace(hint)) row.Children.Add(Caption(hint).Grid(column: 1));
        return row;
    }

    private static Grid InputWithIcon(string glyph, TextBox input) =>
        new Grid().Children(
            input,
            Icon(glyph, 17, SecondaryText)
                .HorizontalAlignment(HorizontalAlignment.Left).VerticalAlignment(VerticalAlignment.Center)
                .Margin(new Thickness(12, 0, 0, 0)).IsHitTestVisible(false));

    private static TextBox FieldTextBox(string placeholder, double rightPadding = 12, double leftPadding = 36) =>
        new TextBox()
            .Style(StaticResource.Get<Style>("TechStoreTextBoxStyle"))
            .Height(40).Padding(new Thickness(leftPadding, 10, rightPadding, 0))
            .VerticalContentAlignment(VerticalAlignment.Top)
            .CornerRadius(new CornerRadius(8))
            .Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(InputStroke))
            .PlaceholderText(placeholder);

    private static TextBlock Error(TextBlock textBlock) =>
        textBlock.Style(StaticResource.Get<Style>("TechStoreFieldErrorStyle"));

    private static UIElement Supporting(string glyph, string text) =>
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(7)
            .Children(
                Icon(glyph, 14, SecondaryText).Margin(new Thickness(0, 2, 0, 0)),
                Caption(text));

    private static UIElement InformationBanner() =>
        new Border()
            .Background(ThemeResource.Get<Brush>(Subtle))
            .CornerRadius(new CornerRadius(8)).Padding(12)
            .Child(new Grid().ColumnSpacing(10)
                .ColumnDefinitions(
                    new ColumnDefinition().Width(GridLength.Auto),
                    new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)))
                .Children(
                    Icon("\uE895", 17, Primary).Margin(new Thickness(0, 2, 0, 0)).Grid(column: 0),
                    new TextBlock().Style(StaticResource.Get<Style>("TechStoreBodyTextStyle"))
                        .Foreground(ThemeResource.Get<Brush>(SecondaryText))
                        .TextWrapping(TextWrapping.Wrap)
                        .Text("Thông tin liên hệ được cập nhật trên hồ sơ khách hàng; lịch sử đơn hàng và điểm tích lũy vẫn được giữ nguyên.")
                        .Grid(column: 1)));

    private static TextBlock Caption(string text) =>
        new TextBlock().Style(StaticResource.Get<Style>("TechStoreCaptionTextStyle"))
            .Foreground(ThemeResource.Get<Brush>(SecondaryText))
            .TextWrapping(TextWrapping.Wrap).Text(text);

    private static Border Badge(string text, string background, string foreground) =>
        new Border().Background(ThemeResource.Get<Brush>(background))
            .CornerRadius(new CornerRadius(4)).Padding(new Thickness(7, 3, 7, 3))
            .Child(new TextBlock().FontSize(11).FontWeight(FontWeights.SemiBold)
                .Foreground(ThemeResource.Get<Brush>(foreground)).Text(text));

    private static FontIcon Icon(string glyph, double size, string foreground) =>
        new FontIcon().Glyph(glyph).FontSize(size)
            .Foreground(ThemeResource.Get<Brush>(foreground))
            .HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center);
}
