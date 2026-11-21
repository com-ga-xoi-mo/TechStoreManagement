using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Sales;
using TechStore.Client.Views.Customers;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Orders.OrderViewParts;

namespace TechStore.Client.Views.Sales;

/// <summary>
/// Cash register: open a shift, scan items, take cash, close the shift. The page only declares the tree and bindings;
/// every rule lives in <see cref="PosViewModel"/>.
/// </summary>
public sealed class PosPage : Page, IRoutePage
{
    public PosViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public PosPage(PosViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        this.Background(AppColors.Brushes.Canvas)
            .Content(
                new ScrollViewer()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .Content(
                        new StackPanel()
                            .Padding(24)
                            .Spacing(16)
                            .Children(
                                BuildHeader(),
                                BuildLoading(),
                                BuildError(),
                                BuildContent())));
    }

    private UIElement BuildHeader() =>
        new Grid()
            .ColumnSpacing(16)
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                new StackPanel()
                    .Spacing(4)
                    .Children(
                        new TextBlock().Style(AppResources.GetStyle(AppTypography.HeadingText))
                            .Foreground(AppColors.Brushes.Text).Text("Bán hàng tại quầy"),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(x => x.Binding(() => ViewModel.ShiftSummary))
                            .Visibility(x => x.Binding(() => ViewModel.HasShift).Convert((bool v) => ShowIf(v)))),
                new Button()
                    .Style(AppControlStyles.Styles.Button)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Content("Đóng ca")
                    .Command(x => x.Binding(() => ViewModel.BeginCloseShiftCommand))
                    .Visibility(x => x.Binding(() => ViewModel.ShowSale).Convert((bool v) => ShowIf(v)))
                    .Grid(column: 1));

    private UIElement BuildLoading() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(12)
            .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => ShowIf(v)))
            .Children(
                new ProgressRing().Width(28).Height(28).IsActive(x => x.Binding(() => ViewModel.IsLoading)),
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Text("Đang tải ca làm việc..."));

    private UIElement BuildError() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock()
                            .Style(AppControlStyles.Styles.FieldError)
                            .Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                        new Button()
                            .Style(AppControlStyles.Styles.PrimaryButton)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Thử tải lại")
                            .Command(x => x.Binding(() => ViewModel.RetryCommand))));

    private UIElement BuildContent() =>
        new StackPanel()
            .Spacing(16)
            .Visibility(x => x.Binding(() => ViewModel.IsContent).Convert((bool v) => ShowIf(v)))
            .Children(
                BuildOpenShift(),
                BuildCloseShift(),
                BuildReceipt(),
                BuildSale());

    // ---------- shift ----------

    private UIElement BuildOpenShift() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.NoShift).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("Chưa mở ca"),
                        Caption("Nhập tiền mặt có trong két lúc bắt đầu ca để mở ca và bắt đầu bán hàng."),
                        new TextBox()
                            .Style(AppControlStyles.Styles.TextBox)
                            .Header("Tiền đầu ca (₫)")
                            .PlaceholderText("0")
                            .Width(240)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Text(x => x.Binding(() => ViewModel.OpeningText).Mode(BindingMode.TwoWay)),
                        ShiftErrorText(),
                        new Button()
                            .Style(AppControlStyles.Styles.PrimaryButton)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Mở ca")
                            .Command(x => x.Binding(() => ViewModel.OpenShiftCommand))));

    private UIElement BuildCloseShift() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.IsClosingShift).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("Đóng ca"),
                        Caption("Tiền dự kiến trong két"),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.MetricText))
                            .Foreground(AppColors.Brushes.Text)
                            .Text(x => x.Binding(() => ViewModel.ExpectedCashText)),
                        new TextBox()
                            .Style(AppControlStyles.Styles.TextBox)
                            .Header("Tiền thực đếm (₫)")
                            .Width(240)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Text(x => x.Binding(() => ViewModel.CountedCashText).Mode(BindingMode.TwoWay)),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.Text)
                            .Text(x => x.Binding(() => ViewModel.DiscrepancyText)),
                        new TextBox()
                            .Style(AppControlStyles.Styles.TextBox)
                            .Header("Lý do chênh lệch (bắt buộc khi tiền thực đếm khác dự kiến)")
                            .AcceptsReturn(true)
                            .TextWrapping(TextWrapping.Wrap)
                            .MinHeight(72)
                            .Text(x => x.Binding(() => ViewModel.ClosingNote).Mode(BindingMode.TwoWay))
                            .Visibility(x => x.Binding(() => ViewModel.NeedsClosingNote).Convert((bool v) => ShowIf(v))),
                        ShiftErrorText(),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new Button()
                                    .Style(AppControlStyles.Styles.PrimaryButton)
                                    .Content("Xác nhận đóng ca")
                                    .Command(x => x.Binding(() => ViewModel.ConfirmCloseShiftCommand)),
                                new Button()
                                    .Style(AppControlStyles.Styles.Button)
                                    .Content("Hủy")
                                    .Command(x => x.Binding(() => ViewModel.CancelCloseShiftCommand)))));

    private TextBlock ShiftErrorText() =>
        new TextBlock()
            .Style(AppControlStyles.Styles.FieldError)
            .Text(x => x.Binding(() => ViewModel.ShiftError))
            .Visibility(x => x.Binding(() => ViewModel.HasShiftError).Convert((bool v) => ShowIf(v)));

    private UIElement BuildReceipt() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.HasReceipt).Convert((bool v) => ShowIf(v)))
            .Child(
                new Grid()
                    .ColumnSpacing(12)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.Text)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(x => x.Binding(() => ViewModel.ReceiptText))
                            .Grid(column: 0),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .Content("Đóng")
                            .Command(x => x.Binding(() => ViewModel.DismissReceiptCommand))
                            .Grid(column: 1)));

    // ---------- selling ----------

    private UIElement BuildSale() =>
        new Grid()
            .ColumnSpacing(16)
            .Visibility(x => x.Binding(() => ViewModel.ShowSale).Convert((bool v) => ShowIf(v)))
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(new GridLength(340, GridUnitType.Pixel)))
            .Children(
                new StackPanel()
                    .Spacing(16)
                    .Children(BuildScan(), BuildCart(), BuildCustomer())
                    .Grid(column: 0),
                BuildPayment().Grid(column: 1));

    private UIElement BuildCustomer()
    {
        if (ViewModel.Customer is not { } customer) return new Border();
        var picker = new CustomerPickerView(customer);
        picker.SetBinding(Control.IsEnabledProperty, new Binding { Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.CanEditCart)) });
        return picker;
    }

    private UIElement BuildScan() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new StackPanel()
                    .Spacing(8)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("Quét hàng"),
                        new SearchBoxControl()
                            .WithInputHeight(44)
                            .Label("IMEI, serial, SKU hoặc mã vạch")
                            .PlaceholderText("Quét hoặc nhập mã rồi nhấn Enter")
                            .Text(x => x.Binding(() => ViewModel.ScanText).Mode(BindingMode.TwoWay))
                            .SearchCommand(x => x.Binding(() => ViewModel.ScanCommand)),
                        new ProgressBar()
                            .IsIndeterminate(true)
                            .Visibility(x => x.Binding(() => ViewModel.IsScanning).Convert((bool v) => ShowIf(v))),
                        new TextBlock()
                            .Style(AppControlStyles.Styles.FieldError)
                            .Text(x => x.Binding(() => ViewModel.ScanMessage))
                            .Visibility(x => x.Binding(() => ViewModel.HasScanMessage).Convert((bool v) => ShowIf(v)))));

    private UIElement BuildCart() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("Giỏ hàng"),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .Text("Giỏ hàng trống. Quét một mã để thêm hàng.")
                            .Visibility(x => x.Binding(() => ViewModel.HasLines).Convert((bool v) => ShowIf(!v))),
                        BuildLineTable()));

    private UIElement BuildLineTable() =>
        new StackPanel()
            .Visibility(x => x.Binding(() => ViewModel.HasLines).Convert((bool v) => ShowIf(v)))
            .Children(
                new ItemsControl()
                    .ItemsSource(x => x.Binding(() => ViewModel.Lines))
                    .ItemTemplate<DraftLineItem>(BuildLine));

    // Two columns only: the name takes what is left and the controls keep their own width. A table with four fixed
    // columns left the name about 30 px at a 1010 px window and the whole row painted blank on Skia.
    private UIElement BuildLine(DraftLineItem item)
    {
        var grid = new Grid().ColumnSpacing(12).Padding(new Thickness(0, 8, 0, 8));
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(
            new StackPanel()
                .Spacing(2)
                .Children(
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.BodyText))
                        .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                        .TextWrapping(TextWrapping.Wrap)
                        .Text(x => x.Binding(() => item.Title)),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.CaptionText))
                        .Foreground(AppColors.Brushes.SecondaryText)
                        .TextWrapping(TextWrapping.Wrap)
                        .Text(x => x.Binding(() => item.Detail)),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.CaptionText))
                        .Foreground(AppColors.Brushes.SecondaryText)
                        .Text(x => x.Binding(() => item.UnitPrice)),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.CaptionText))
                        .Foreground(AppColors.Brushes.DangerText)
                        .TextWrapping(TextWrapping.Wrap)
                        .Text(x => x.Binding(() => item.Error))
                        .Visibility(x => x.Binding(() => item.HasError).Convert((bool v) => ShowIf(v))))
                .Grid(column: 0));
        grid.Children.Add(
            new StackPanel()
                .Orientation(Orientation.Horizontal)
                .Spacing(6)
                .VerticalAlignment(VerticalAlignment.Center)
                .Children(
                    new Button()
                        .Style(AppControlStyles.Styles.QuietButton)
                        .Content("−")
                        .Command(ViewModel.DecreaseQuantityCommand)
                        .CommandParameter(x => x.Binding(() => item))
                        .Visibility(x => x.Binding(() => item.CanChangeQuantity).Convert((bool v) => ShowIf(v))),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.BodyText))
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Text(x => x.Binding(() => item.Quantity)),
                    new Button()
                        .Style(AppControlStyles.Styles.QuietButton)
                        .Content("+")
                        .Command(ViewModel.IncreaseQuantityCommand)
                        .CommandParameter(x => x.Binding(() => item))
                        .Visibility(x => x.Binding(() => item.CanChangeQuantity).Convert((bool v) => ShowIf(v))),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.BodyText))
                        .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .MinWidth(110)
                        .Text(x => x.Binding(() => item.Total)),
                    new Button()
                        .Style(AppControlStyles.Styles.QuietButton)
                        .Content("Xóa")
                        .Command(ViewModel.RemoveLineCommand)
                        .CommandParameter(x => x.Binding(() => item)))
                .Grid(column: 1));

        return new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Child(grid);
    }

    // ---------- cash payment ----------

    private UIElement BuildPayment()
    {
        // Adding these controls directly avoids recursive ResourceParent traversal on first layout in Uno Skia 6.7.
        var panel = new StackPanel().Spacing(14);
        panel.Children.Add(new TextBlock().Style(AppTypography.Styles.SectionText).Text("Thanh toán tiền mặt"));
        panel.Children.Add(new TextBox().Style(AppControlStyles.Styles.TextBox)
            .Header("Giảm giá đơn (₫)")
            .IsEnabled(x => x.Binding(() => ViewModel.CanEditCart))
            .Text(x => x.Binding(() => ViewModel.DiscountText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)));
        panel.Children.Add(Caption("Thuế VAT"));
        panel.Children.Add(new TextBlock().Text(x => x.Binding(() => ViewModel.TaxText)));
        panel.Children.Add(new TextBlock().Style(AppControlStyles.Styles.FieldError)
            .TextWrapping(TextWrapping.Wrap)
            .Text(x => x.Binding(() => ViewModel.QuoteError))
            .Visibility(x => x.Binding(() => ViewModel.HasQuoteError).Convert((bool v) => ShowIf(v))));
        panel.Children.Add(new Button().Content("Tính lại")
            .Command(x => x.Binding(() => ViewModel.RefreshQuoteCommand))
            .Visibility(x => x.Binding(() => ViewModel.HasQuoteError).Convert((bool v) => ShowIf(v))));
        panel.Children.Add(new StackPanel()
            .Spacing(2)
            .Children(
                Caption("Tổng thanh toán"),
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.MetricText))
                    .Foreground(AppColors.Brushes.Text)
                    .Text(x => x.Binding(() => ViewModel.EstimatedTotalText))));
        panel.Children.Add(new TextBox()
            .Style(AppControlStyles.Styles.TextBox)
            .Header("Tiền khách đưa (₫)")
            .IsEnabled(x => x.Binding(() => ViewModel.CanEditCart))
            .Text(x => x.Binding(() => ViewModel.CashText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)));
        panel.Children.Add(new StackPanel()
            .Spacing(2)
            .Children(
                Caption("Tiền thừa trả khách"),
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.MetricText))
                    .Foreground(AppColors.Brushes.Text)
                    .Text(x => x.Binding(() => ViewModel.ChangeText))));
        panel.Children.Add(new TextBlock()
            .Style(AppControlStyles.Styles.FieldError)
            .Text(x => x.Binding(() => ViewModel.PayError))
            .Visibility(x => x.Binding(() => ViewModel.HasPayError).Convert((bool v) => ShowIf(v))));
        panel.Children.Add(new Button()
            .Style(AppControlStyles.Styles.PrimaryButton)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Content("Thanh toán")
            .Command(x => x.Binding(() => ViewModel.PayCommand)));
        panel.Children.Add(new Button()
            .Style(AppControlStyles.Styles.Button)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Content("Xóa giỏ hàng")
            .Command(x => x.Binding(() => ViewModel.ClearCartCommand)));
        panel.Children.Add(new ProgressBar()
            .IsIndeterminate(true)
            .Visibility(x => x.Binding(() => ViewModel.IsPaying).Convert((bool v) => ShowIf(v))));

        return new Border().Style(AppControlStyles.Styles.Panel)
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(panel);
    }
}
