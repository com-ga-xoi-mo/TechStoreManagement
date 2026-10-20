using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Orders.OrderViewParts;

namespace TechStore.Client.Views.Orders;

/// <summary>
/// Counter order entry: find a product, pick variant and device, build the lines, then save or pay and complete.
/// The page only declares the tree and bindings; every rule lives in <see cref="OrderCreateViewModel"/>.
/// </summary>
public sealed class OrderCreatePage : Page, IRoutePage
{
    public OrderCreateViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public OrderCreatePage(OrderCreateViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        this.Background(AppColors.Brushes.Canvas)
            .Content(
                new ScrollViewer()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .Content(
                        new Grid()
                            .Padding(24)
                            .RowSpacing(16)
                            .RowDefinitions(
                                new RowDefinition().Height(GridLength.Auto),
                                new RowDefinition().Height(GridLength.Auto))
                            .Children(
                                BuildHeader().Grid(row: 0),
                                new Grid()
                                    .ColumnSpacing(16)
                                    .ColumnDefinitions(
                                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                                        new ColumnDefinition().Width(new GridLength(380, GridUnitType.Pixel)))
                                    .Children(
                                        new StackPanel()
                                            .Spacing(16)
                                            .Children(BuildFinder(), BuildEditor(), BuildLines())
                                            .Grid(column: 0),
                                        BuildSummary().Grid(column: 1))
                                    .Grid(row: 1))));
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
                        new TextBlock().Style(AppTypography.Styles.HeadingText).Text("Lập đơn hàng"),
                        Caption("Khách lẻ. Giá lấy từ danh mục; số tiền dưới đây là tạm tính, hệ thống sẽ tính lại khi tạo đơn.")),
                new Button()
                    .Style(AppControlStyles.Styles.Button)
                    .Content("Quay lại danh sách")
                    .VerticalAlignment(VerticalAlignment.Top)
                    .Command(x => x.Binding(() => ViewModel.BackToListCommand))
                    .Grid(column: 1));

    // ---------- 1. find a product ----------

    private UIElement BuildFinder() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("1. Chọn sản phẩm"),
                        new SearchBoxControl()
                            .Label("Tìm sản phẩm")
                            .PlaceholderText("Tên, SKU hoặc mã vạch")
                            .Text(x => x.Binding(() => ViewModel.ProductSearchText).Mode(BindingMode.TwoWay))
                            .SearchCommand(x => x.Binding(() => ViewModel.SearchProductsCommand))
                            .ClearCommand(x => x.Binding(() => ViewModel.ClearProductSearchCommand)),
                        new ProgressBar()
                            .IsIndeterminate(true)
                            .Visibility(x => x.Binding(() => ViewModel.IsSearchingProducts).Convert((bool v) => ShowIf(v))),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(x => x.Binding(() => ViewModel.ProductSearchMessage))
                            .Visibility(x => x.Binding(() => ViewModel.HasProductSearchMessage).Convert((bool v) => ShowIf(v))),
                        BuildProductResults()));

    private UIElement BuildProductResults() =>
        new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.ProductResults))
            .ItemTemplate<ProductChoice>(BuildProductResult);

    private UIElement BuildProductResult(ProductChoice choice) =>
        new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 8, 0, 8))
            .Child(
                new Grid()
                    .ColumnSpacing(12)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(
                        new StackPanel()
                            .Spacing(2)
                            .Children(
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                                    .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                                    .Text(x => x.Binding(() => choice.Title)),
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .Text(x => x.Binding(() => choice.Subtitle)))
                            .Grid(column: 0),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(x => x.Binding(() => choice.PriceText))
                            .Grid(column: 1),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .Content("Chọn")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Command(ViewModel.SelectProductCommand)
                            .CommandParameter(x => x.Binding(() => choice.Summary))
                            .Grid(column: 2)));

    // ---------- 2. variant, device, quantity ----------

    private UIElement BuildEditor() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.HasProduct).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("2. Biến thể, máy và số lượng"),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.Text)
                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                            .Text(x => x.Binding(() => ViewModel.SelectedProductName)),
                        new ComboBox()
                            .Style(AppControlStyles.Styles.ComboBox)
                            .Header("Biến thể")
                            .MinWidth(280)
                            .ItemsSource(x => x.Binding(() => ViewModel.Variants))
                            .DisplayMemberPath("VariantName")
                            .SelectedItem(x => x.Binding(() => ViewModel.SelectedVariant).Mode(BindingMode.TwoWay))
                            .Visibility(x => x.Binding(() => ViewModel.ShowVariantPicker).Convert((bool v) => ShowIf(v))),
                        new StackPanel()
                            .Spacing(8)
                            .Visibility(x => x.Binding(() => ViewModel.ShowDevicePicker).Convert((bool v) => ShowIf(v)))
                            .Children(
                                new ComboBox()
                                    .Style(AppControlStyles.Styles.ComboBox)
                                    .Header("Máy (IMEI/serial) còn trong kho")
                                    .MinWidth(280)
                                    .ItemsSource(x => x.Binding(() => ViewModel.Devices))
                                    .DisplayMemberPath("Display")
                                    .SelectedItem(x => x.Binding(() => ViewModel.SelectedDevice).Mode(BindingMode.TwoWay)),
                                new ProgressBar()
                                    .IsIndeterminate(true)
                                    .Visibility(x => x.Binding(() => ViewModel.IsLoadingDevices).Convert((bool v) => ShowIf(v))),
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Text(x => x.Binding(() => ViewModel.DeviceNotice))
                                    .Visibility(x => x.Binding(() => ViewModel.HasDeviceNotice).Convert((bool v) => ShowIf(v)))),
                        new TextBox()
                            .Style(AppControlStyles.Styles.TextBox)
                            .Header("Số lượng")
                            .Width(160)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Text(x => x.Binding(() => ViewModel.QuantityText).Mode(BindingMode.TwoWay))
                            .Visibility(x => x.Binding(() => ViewModel.ShowQuantity).Convert((bool v) => ShowIf(v))),
                        new TextBox()
                            .Style(AppControlStyles.Styles.TextBox)
                            .Header("Giảm giá cho dòng này (₫)")
                            .PlaceholderText("0")
                            .Width(240)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Text(x => x.Binding(() => ViewModel.DiscountText).Mode(BindingMode.TwoWay)),
                        new TextBlock()
                            .Style(AppControlStyles.Styles.FieldError)
                            .Text(x => x.Binding(() => ViewModel.LineError))
                            .Visibility(x => x.Binding(() => ViewModel.HasLineError).Convert((bool v) => ShowIf(v))),
                        new Button()
                            .Style(AppControlStyles.Styles.PrimaryButton)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Thêm vào đơn")
                            .Command(x => x.Binding(() => ViewModel.AddLineCommand))));

    // ---------- the lines ----------

    private UIElement BuildLines() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new Grid()
                            .ColumnDefinitions(
                                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                                new ColumnDefinition().Width(GridLength.Auto))
                            .Children(
                                new TextBlock().Style(AppTypography.Styles.SectionText).Text("Các dòng trong đơn"),
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .Text(x => x.Binding(() => ViewModel.LineCountText))
                                    .Grid(column: 1)),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .Text("Chưa có dòng nào. Tìm sản phẩm ở trên để thêm.")
                            .Visibility(x => x.Binding(() => ViewModel.HasLines).Convert((bool v) => ShowIf(!v))),
                        BuildLineTable()));

    private UIElement BuildLineTable()
    {
        var header = NewLineGrid();
        header.Children.Add(HeaderText("SẢN PHẨM").Grid(column: 0));
        header.Children.Add(HeaderText("SL").HorizontalAlignment(HorizontalAlignment.Right).Grid(column: 1));
        header.Children.Add(HeaderText("ĐƠN GIÁ").HorizontalAlignment(HorizontalAlignment.Right).Grid(column: 2));
        header.Children.Add(HeaderText("GIẢM").HorizontalAlignment(HorizontalAlignment.Right).Grid(column: 3));
        header.Children.Add(HeaderText("THÀNH TIỀN").HorizontalAlignment(HorizontalAlignment.Right).Grid(column: 4));

        return new StackPanel()
            .Visibility(x => x.Binding(() => ViewModel.HasLines).Convert((bool v) => ShowIf(v)))
            .Children(
                new Border()
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(0, 0, 0, 1))
                    .Child(header),
                new ItemsControl()
                    .ItemsSource(x => x.Binding(() => ViewModel.Lines))
                    .ItemTemplate<DraftLineItem>(BuildLine));
    }

    private UIElement BuildLine(DraftLineItem item)
    {
        var grid = NewLineGrid();
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
                        .Foreground(AppColors.Brushes.DangerText)
                        .TextWrapping(TextWrapping.Wrap)
                        .Text(x => x.Binding(() => item.Error))
                        .Visibility(x => x.Binding(() => item.HasError).Convert((bool v) => ShowIf(v))))
                .Grid(column: 0));
        grid.Children.Add(RightText(x => x.Binding(() => item.Quantity)).Grid(column: 1));
        grid.Children.Add(RightText(x => x.Binding(() => item.UnitPrice)).Grid(column: 2));
        grid.Children.Add(RightText(x => x.Binding(() => item.Discount)).Grid(column: 3));
        grid.Children.Add(RightText(x => x.Binding(() => item.Total)).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold).Grid(column: 4));
        grid.Children.Add(
            new Button()
                .Style(AppControlStyles.Styles.QuietButton)
                .Content("Xóa")
                .Command(ViewModel.RemoveLineCommand)
                .CommandParameter(x => x.Binding(() => item))
                .Grid(column: 5));

        return new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Child(grid);
    }

    private static TextBlock RightText(Action<IDependencyPropertyBuilder<string>> text) =>
        new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .HorizontalAlignment(HorizontalAlignment.Right)
            .Text(text);

    private static Grid NewLineGrid()
    {
        var grid = new Grid().ColumnSpacing(12).Padding(new Thickness(0, 8, 0, 8));
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64, GridUnitType.Pixel) });
        return grid;
    }

    // ---------- payment, notes, total, submit ----------

    private UIElement BuildSummary() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(14)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text("Thanh toán"),
                        new ComboBox()
                            .Style(AppControlStyles.Styles.ComboBox)
                            .Header("Phương thức")
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .ItemsSource(ViewModel.PaymentOptions)
                            .DisplayMemberPath("Label")
                            .SelectedItem(x => x.Binding(() => ViewModel.SelectedPayment).Mode(BindingMode.TwoWay)),
                        new TextBox()
                            .Style(AppControlStyles.Styles.TextBox)
                            .Header("Ghi chú (tối đa 500 ký tự)")
                            .AcceptsReturn(true)
                            .TextWrapping(TextWrapping.Wrap)
                            .MinHeight(72)
                            .Text(x => x.Binding(() => ViewModel.Notes).Mode(BindingMode.TwoWay)),
                        new Border()
                            .BorderBrush(AppColors.Brushes.Stroke)
                            .BorderThickness(new Thickness(0, 1, 0, 0))
                            .Padding(new Thickness(0, 12, 0, 0))
                            .Child(
                                new StackPanel()
                                    .Spacing(2)
                                    .Children(
                                        Caption("Tạm tính"),
                                        new TextBlock()
                                            .Style(AppTypography.Styles.MetricText)
                                            .Text(x => x.Binding(() => ViewModel.EstimatedTotalText)))),
                        new TextBlock()
                            .Style(AppControlStyles.Styles.FieldError)
                            .Text(x => x.Binding(() => ViewModel.GeneralError))
                            .Visibility(x => x.Binding(() => ViewModel.HasGeneralError).Convert((bool v) => ShowIf(v))),
                        BuildPartialFailure(),
                        new Button()
                            .Style(AppControlStyles.Styles.PrimaryButton)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Content("Thanh toán & hoàn tất")
                            .Command(x => x.Binding(() => ViewModel.PayAndCompleteCommand)),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Content("Lưu đơn nháp")
                            .Command(x => x.Binding(() => ViewModel.SaveDraftCommand)),
                        new ProgressBar()
                            .IsIndeterminate(true)
                            .Visibility(x => x.Binding(() => ViewModel.IsSubmitting).Convert((bool v) => ShowIf(v)))));

    private UIElement BuildPartialFailure() =>
        new Border()
            .Background(AppColors.Brushes.DangerFill)
            .BorderBrush(AppColors.Brushes.DangerStroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(12)
            .Visibility(x => x.Binding(() => ViewModel.HasPartialFailure).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Spacing(10)
                    .Children(
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.DangerText)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(x => x.Binding(() => ViewModel.PartialFailureMessage)),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Về danh sách đơn")
                            .Command(x => x.Binding(() => ViewModel.BackToListCommand))));
}
