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

public sealed class CustomerDeactivationDialogView : UserControl
{
    private const string Surface = AppColors.Surface;
    private const string Subtle = AppColors.Subtle;
    private const string Text = AppColors.Text;
    private const string SecondaryText = AppColors.SecondaryText;
    private const string Stroke = AppColors.Stroke;
    private const string DangerFill = AppColors.DangerFill;
    private const string DangerText = AppColors.DangerText;
    private const string SuccessText = AppColors.SuccessText;

    public CustomerListViewModel ViewModel { get; }
    public IAsyncRelayCommand ConfirmCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public Control InitialFocusTarget { get; }

    public CustomerDeactivationDialogView(
        CustomerListViewModel viewModel,
        IAsyncRelayCommand confirmCommand,
        IRelayCommand closeCommand)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        ConfirmCommand = confirmCommand ?? throw new ArgumentNullException(nameof(confirmCommand));
        CloseCommand = closeCommand ?? throw new ArgumentNullException(nameof(closeCommand));
        DataContext = ViewModel;

        var closeButton = new Button()
            .Width(28).Height(28).Padding(0)
            .VerticalAlignment(VerticalAlignment.Top).Margin(new Thickness(0, 6, 0, 0))
            .Background(ThemeResource.Get<Brush>(Surface))
            .BorderThickness(0)
            .Content(new FontIcon().Glyph("\uE711").FontSize(14)
                .Foreground(ThemeResource.Get<Brush>(SecondaryText)))
            .Command(CloseCommand);
        AutomationProperties.SetName(closeButton, "Đóng xác nhận vô hiệu hóa");
        closeButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape });

        var cancelButton = new Button()
            .MinWidth(76).Height(36)
            .CornerRadius(new CornerRadius(6))
            .Background(ThemeResource.Get<Brush>(Surface))
            .Foreground(ThemeResource.Get<Brush>(Text))
            .BorderThickness(0)
            .Content("Hủy")
            .Command(CloseCommand);
        InitialFocusTarget = cancelButton;

        var confirmButton = new Button()
            .MinWidth(126).Height(36)
            .CornerRadius(new CornerRadius(6))
            .Background(ThemeResource.Get<Brush>(DangerText))
            .Foreground(ThemeResource.Get<Brush>(Surface))
            .BorderThickness(0)
            .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(7)
                .Children(
                    new FontIcon().Glyph("\uE74D").FontSize(14),
                    new TextBlock().Text("Vô hiệu hóa").VerticalAlignment(VerticalAlignment.Center)))
            .Command(ConfirmCommand);
        AutomationProperties.SetName(confirmButton, "Xác nhận vô hiệu hóa khách hàng");

        var warningIcon = new Border()
            .Width(40).Height(40).CornerRadius(new CornerRadius(10))
            .VerticalAlignment(VerticalAlignment.Top).Margin(new Thickness(0, 6, 0, 0))
            .Background(ThemeResource.Get<Brush>(DangerFill))
            .Child(new FontIcon().Glyph("\uE783").FontSize(20)
                .Foreground(ThemeResource.Get<Brush>(DangerText))
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Center));

        var details = new StackPanel().Spacing(10)
            .Children(
                new TextBlock().FontSize(17).FontWeight(FontWeights.SemiBold)
                    .TextWrapping(TextWrapping.Wrap)
                    .Foreground(ThemeResource.Get<Brush>(Text))
                    .Text(x => x.Binding(() => ViewModel.CustomerToDeactivate)
                        .Convert((CustomerSummary? customer) => customer is null
                            ? "Vô hiệu hóa khách hàng?"
                            : $"Vô hiệu hóa khách hàng {customer.FullName}?")),
                new TextBlock().FontSize(13).TextWrapping(TextWrapping.Wrap)
                    .Foreground(ThemeResource.Get<Brush>(SecondaryText))
                    .Text("Khách hàng sẽ bị ẩn khỏi danh sách đang hoạt động và không thể tạo đơn mới. Hồ sơ, đơn hàng và điểm tích lũy vẫn được giữ nguyên."),
                Summary(),
                new TextBlock().FontSize(12).TextWrapping(TextWrapping.Wrap)
                    .Foreground(ThemeResource.Get<Brush>(DangerText))
                    .Text(x => x.Binding(() => ViewModel.ActionErrorMessage))
                    .Visibility(x => x.Binding(() => ViewModel.HasActionError)
                        .Convert((bool hasError) => hasError ? Visibility.Visible : Visibility.Collapsed)));

        var body = new Grid().Padding(new Thickness(24, 22, 24, 24)).ColumnSpacing(14)
            .ColumnDefinitions(
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                warningIcon.Grid(column: 0),
                details.Grid(column: 1),
                closeButton.Grid(column: 2));

        var footer = new Border()
            .Background(ThemeResource.Get<Brush>(Subtle))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke))
            .BorderThickness(new Thickness(0, 1, 0, 0))
            .Padding(new Thickness(24, 14, 24, 14))
            .Child(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8)
                .HorizontalAlignment(HorizontalAlignment.Right)
                .Children(cancelButton, confirmButton));

        Content = new Border()
            .Width(490)
            .Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke))
            .BorderThickness(1)
            .CornerRadius(new CornerRadius(12))
            .Child(new Grid().RowDefinitions(
                    new RowDefinition().Height(GridLength.Auto),
                    new RowDefinition().Height(GridLength.Auto))
                .Children(body.Grid(row: 0), footer.Grid(row: 1)));
    }

    private UIElement Summary()
    {
        var header = new Grid().ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                new TextBlock().Text("Thông tin được giữ lại:")
                    .FontSize(11).FontWeight(FontWeights.SemiBold)
                    .Foreground(ThemeResource.Get<Brush>(SecondaryText)).Grid(column: 0),
                new TextBlock().Text("Có thể kích hoạt lại")
                    .FontSize(11).FontWeight(FontWeights.SemiBold)
                    .Foreground(ThemeResource.Get<Brush>(SuccessText)).Grid(column: 1));

        return new Border()
            .Background(ThemeResource.Get<Brush>(Subtle))
            .CornerRadius(new CornerRadius(6))
            .Padding(new Thickness(10, 9, 10, 9))
            .Child(new StackPanel().Spacing(6)
                .Children(
                    header,
                    SummaryRow("Điểm tích lũy:", customer => $"{customer.LoyaltyPoints:N0} điểm"),
                    SummaryRow("Đơn hàng đã lưu:", customer => $"{customer.OrderCount:N0} đơn")));
    }

    private Grid SummaryRow(string label, Func<CustomerSummary, string> value) =>
        new Grid().ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                new TextBlock().Text(label).FontSize(12)
                    .Foreground(ThemeResource.Get<Brush>(Text)).Grid(column: 0),
                new TextBlock().FontSize(12).FontWeight(FontWeights.SemiBold)
                    .Foreground(ThemeResource.Get<Brush>(Text))
                    .Text(x => x.Binding(() => ViewModel.CustomerToDeactivate)
                        .Convert((CustomerSummary? customer) => customer is null ? "—" : value(customer)))
                    .Grid(column: 1));
}
