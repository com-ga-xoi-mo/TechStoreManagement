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

/// <summary>Order list: search, filter, sort, page, row actions. The page only declares the tree and bindings.</summary>
public sealed class OrderListPage : Page, IRoutePage
{
    public OrderListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public OrderListPage(OrderListViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        this.Background(AppColors.Brushes.Canvas)
            .Content(
                new Grid()
                    .Padding(24)
                    .RowSpacing(16)
                    .RowDefinitions(
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(new GridLength(1, GridUnitType.Star)))
                    .Children(
                        BuildHeader().Grid(row: 0),
                        BuildToolbar().Grid(row: 1),
                        BuildActionMessage().Grid(row: 2),
                        BuildBody().Grid(row: 3)));
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
                        new TextBlock().Style(AppTypography.Styles.HeadingText).Text("Danh sách đơn hàng"),
                        Caption("Tìm, lọc và xử lý đơn bán tại quầy. Máy có IMEI chỉ chốt khi hoàn tất đơn.")),
                new Button()
                    .Style(AppControlStyles.Styles.PrimaryButton)
                    .Content("Lập đơn mới")
                    .VerticalAlignment(VerticalAlignment.Top)
                    .Command(x => x.Binding(() => ViewModel.NewOrderCommand))
                    .Grid(column: 1));

    private UIElement BuildToolbar() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new Grid()
                    .ColumnSpacing(12)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(
                        new SearchBoxControl()
                            .Label("Tìm đơn hàng")
                            .PlaceholderText("Mã đơn, tên hoặc số điện thoại khách")
                            .Text(x => x.Binding(() => ViewModel.SearchText).Mode(BindingMode.TwoWay))
                            .SearchCommand(x => x.Binding(() => ViewModel.SearchCommand))
                            .ClearCommand(x => x.Binding(() => ViewModel.ClearSearchCommand))
                            .Grid(column: 0),
                        new ComboBox()
                            .Style(AppControlStyles.Styles.ComboBox)
                            .Header("Trạng thái")
                            .MinWidth(170)
                            .ItemsSource(ViewModel.StatusFilters)
                            .DisplayMemberPath("Label")
                            .SelectedItem(x => x.Binding(() => ViewModel.SelectedStatusFilter).Mode(BindingMode.TwoWay))
                            .Grid(column: 1),
                        new ComboBox()
                            .Style(AppControlStyles.Styles.ComboBox)
                            .Header("Sắp xếp")
                            .MinWidth(170)
                            .ItemsSource(ViewModel.SortOptions)
                            .DisplayMemberPath("Label")
                            .SelectedItem(x => x.Binding(() => ViewModel.SelectedSort).Mode(BindingMode.TwoWay))
                            .Grid(column: 2),
                        new ComboBox()
                            .Style(AppControlStyles.Styles.ComboBox)
                            .Header("Mỗi trang")
                            .MinWidth(90)
                            .ItemsSource(ViewModel.PageSizes)
                            .SelectedItem(x => x.Binding(() => ViewModel.SelectedPageSize).Mode(BindingMode.TwoWay))
                            .Grid(column: 3),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .VerticalAlignment(VerticalAlignment.Bottom)
                            .Content("Đặt lại")
                            .Command(x => x.Binding(() => ViewModel.ResetFiltersCommand))
                            .Grid(column: 4)));

    private UIElement BuildActionMessage() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.HasActionMessage).Convert((bool v) => ShowIf(v)))
            .Child(
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                    .Foreground(AppColors.Brushes.Text)
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(x => x.Binding(() => ViewModel.ActionMessage)));

    // Exactly one area shows at a time: first load, error, empty, or the list.
    private UIElement BuildBody() =>
        new Grid()
            .Children(
                BuildInitialLoading(),
                BuildError(),
                BuildEmpty(),
                BuildList());

    private UIElement BuildInitialLoading() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(12)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => ViewModel.ShowInitialLoading).Convert((bool v) => ShowIf(v)))
            .Children(
                new ProgressRing().Width(28).Height(28).IsActive(x => x.Binding(() => ViewModel.ShowInitialLoading)),
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Text("Đang tải đơn hàng..."));

    private UIElement BuildError() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .VerticalAlignment(VerticalAlignment.Top)
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

    private UIElement BuildEmpty() =>
        new EmptyStateControl()
            .Title("Chưa có đơn hàng")
            .Description("Không có đơn nào khớp bộ lọc hiện tại. Hãy đổi bộ lọc hoặc lập đơn mới.")
            .ActionText("Lập đơn mới")
            .ActionCommand(x => x.Binding(() => ViewModel.NewOrderCommand))
            .Visibility(x => x.Binding(() => ViewModel.ShowEmptyState).Convert((bool v) => ShowIf(v)));

    private UIElement BuildList() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Padding(0)
            .Visibility(x => x.Binding(() => ViewModel.ShowList).Convert((bool v) => ShowIf(v)))
            .Child(
                new Grid()
                    .RowDefinitions(
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(new GridLength(1, GridUnitType.Star)),
                        new RowDefinition().Height(GridLength.Auto))
                    .Children(
                        new ProgressBar()
                            .IsIndeterminate(true)
                            .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => ShowIf(v)))
                            .Grid(row: 0),
                        BuildTableHeader().Grid(row: 1),
                        new ScrollViewer()
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .Content(BuildRows())
                            .Grid(row: 2),
                        BuildPagination().Grid(row: 3)));

    private static UIElement BuildTableHeader()
    {
        var header = NewRowGrid();
        header.Children.Add(HeaderText("MÃ ĐƠN").Grid(column: 0));
        header.Children.Add(HeaderText("KHÁCH HÀNG").Grid(column: 1));
        header.Children.Add(HeaderText("TỔNG TIỀN").HorizontalAlignment(HorizontalAlignment.Right).Grid(column: 2));
        header.Children.Add(HeaderText("TRẠNG THÁI").Grid(column: 3));
        header.Children.Add(HeaderText("THAO TÁC").Grid(column: 4));
        return new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Child(header);
    }

    private ItemsControl BuildRows() =>
        new ItemsControl()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .ItemsSource(x => x.Binding(() => ViewModel.Rows))
            .ItemTemplate<OrderRow>(BuildRow);

    // One row. `row` only stands for the item the template is applied to; the lambdas become bindings to its properties.
    private UIElement BuildRow(OrderRow row)
    {
        var grid = NewRowGrid();

        grid.Children.Add(
            new StackPanel()
                .Spacing(2)
                .Children(
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.BodyText))
                        .Foreground(AppColors.Brushes.Primary)
                        .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                        .TextWrapping(TextWrapping.NoWrap)
                        .Text(x => x.Binding(() => row.Code)),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.CaptionText))
                        .Foreground(AppColors.Brushes.SecondaryText)
                        .TextWrapping(TextWrapping.NoWrap)
                        .Text(x => x.Binding(() => row.CreatedAt)))
                .Grid(column: 0));
        grid.Children.Add(
            new StackPanel()
                .Spacing(2)
                .Children(
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.BodyText))
                        .TextWrapping(TextWrapping.NoWrap)
                        .TextTrimming(TextTrimming.CharacterEllipsis)
                        .Text(x => x.Binding(() => row.Customer)),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.CaptionText))
                        .Foreground(AppColors.Brushes.SecondaryText)
                        .TextWrapping(TextWrapping.NoWrap)
                        .Text(x => x.Binding(() => row.PaymentSummary)))
                .Grid(column: 1));
        grid.Children.Add(
            new TextBlock()
                .Style(AppResources.GetStyle(AppTypography.BodyText))
                .HorizontalAlignment(HorizontalAlignment.Right)
                .VerticalAlignment(VerticalAlignment.Center)
                .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                .TextWrapping(TextWrapping.NoWrap)
                .Text(x => x.Binding(() => row.Total))
                .Grid(column: 2));
        grid.Children.Add(
            new StatusBadge()
                .Label(x => x.Binding(() => row.StatusLabel))
                .Tone(x => x.Binding(() => row.StatusTone))
                .HorizontalAlignment(HorizontalAlignment.Left)
                .VerticalAlignment(VerticalAlignment.Center)
                .Grid(column: 3));
        grid.Children.Add(BuildRowActions(row).VerticalAlignment(VerticalAlignment.Center).Grid(column: 4));

        return new Border()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Child(grid);
    }

    private StackPanel BuildRowActions(OrderRow row) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(6)
            .Children(
                new Button()
                    .Style(AppControlStyles.Styles.QuietButton)
                    .Content("Xem")
                    .Command(ViewModel.OpenDetailCommand)
                    .CommandParameter(x => x.Binding(() => row)),
                new Button()
                    .Style(AppControlStyles.Styles.Button)
                    .Content("Xác nhận TT")
                    .Visibility(x => x.Binding(() => row.HasPayAction).Convert((bool v) => ShowIf(v)))
                    .Command(ViewModel.ChangeStatusCommand)
                    .CommandParameter(x => x.Binding(() => row.PayAction)),
                new Button()
                    .Style(AppControlStyles.Styles.PrimaryButton)
                    .Content("Hoàn tất")
                    .Visibility(x => x.Binding(() => row.HasCompleteAction).Convert((bool v) => ShowIf(v)))
                    .Command(ViewModel.ChangeStatusCommand)
                    .CommandParameter(x => x.Binding(() => row.CompleteAction)),
                new Button()
                    .Style(AppControlStyles.Styles.DangerButton)
                    .Content("Hủy")
                    .Visibility(x => x.Binding(() => row.HasCancelAction).Convert((bool v) => ShowIf(v)))
                    .Command(ViewModel.ChangeStatusCommand)
                    .CommandParameter(x => x.Binding(() => row.CancelAction)));

    private UIElement BuildPagination() =>
        new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 1, 0, 0))
            .Padding(new Thickness(16, 10, 16, 10))
            .Visibility(x => x.Binding(() => ViewModel.ShowPagination).Convert((bool v) => ShowIf(v)))
            .Child(
                new Grid()
                    .ColumnSpacing(8)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(x => x.Binding(() => ViewModel.ResultSummary)),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .Content("Trang trước")
                            .Command(x => x.Binding(() => ViewModel.PreviousPageCommand))
                            .Grid(column: 1),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .Content("Trang sau")
                            .Command(x => x.Binding(() => ViewModel.NextPageCommand))
                            .Grid(column: 2)));

    // Header and rows share one column layout so the columns line up. Only the customer column flexes.
    private static Grid NewRowGrid()
    {
        var grid = new Grid().ColumnSpacing(12).Padding(new Thickness(16, 10, 16, 10));
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340, GridUnitType.Pixel) });
        return grid;
    }
}
