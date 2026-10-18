using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation.Peers;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Charts;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Dashboard;

public sealed class DashboardPage : Page, IRoutePage
{
    private readonly DashboardViewModel _viewModel;
    private readonly Canvas _revenueCanvas = new() { Height = 280 };
    private readonly Microsoft.UI.Xaml.Shapes.Path _revenueLine = new() { StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round };
    private Grid? _metricsGrid;
    private Grid? _analyticsGrid;
    private Border? _chartPanel;
    private Border? _stockPanel;
    private Grid? _headerGrid;
    private readonly List<Button> _periodButtons = [];

    public PageViewModel ViewModel => _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _revenueCanvas.Children.Add(_revenueLine);
        AutomationProperties.SetName(_revenueCanvas, "Doanh thu theo ngày; bảng số liệu theo ngày nằm ngay bên dưới.");
        _revenueCanvas.SizeChanged += (_, _) => DrawRevenueChart();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        var periodSelector = BuildPeriodSelector();

        var content = new StackPanel().Spacing(18).Children(
            BuildHeader(periodSelector),
            new InfoBar().IsOpen(x => x.Binding(() => viewModel.ShowPreviewBanner)).IsClosable(false)
                .Severity(InfoBarSeverity.Informational).Title("Dữ liệu minh họa — chưa kết nối Analytics API")
                .Message(x => x.Binding(() => viewModel.ReferenceLabel)),
            new InfoBar().IsOpen(x => x.Binding(() => viewModel.IsError)).IsClosable(false)
                .Severity(InfoBarSeverity.Error).Title("Không tải được Dashboard")
                .Message(x => x.Binding(() => viewModel.ErrorMessage)),
            new Button().Content("Thử lại").Command(x => x.Binding(() => viewModel.RetryCommand))
                .Visibility(x => x.Binding(() => viewModel.IsError).Convert(ShowIf)),
            BuildMetrics(),
            new EmptyStateControl().Title("Dashboard")
                .Description(x => x.Binding(() => viewModel.EmptyDashboardMessage))
                .IconSource(GetIcon(AppIcons.EmptyIcon)!)
                .Visibility(x => x.Binding(() => viewModel.IsEmpty).Convert(ShowIf)),
            BuildAnalyticsPanels());

        this.Background(AppColors.Brushes.Canvas)
            .Content(new ScrollViewer().VerticalScrollBarVisibility(ScrollBarVisibility.Auto).Content(content));
        Loaded += (_, _) => DrawRevenueChart();
        SizeChanged += (_, args) =>
        {
            UpdateMetricsLayout(args.NewSize.Width);
            UpdateAnalyticsLayout(args.NewSize.Width);
            UpdateHeaderLayout(args.NewSize.Width);
        };
        ActualThemeChanged += (_, _) => DrawRevenueChart();
    }

    private UIElement BuildHeader(UIElement periodSelector)
    {
        _headerGrid = new Grid().ColumnSpacing(16).RowSpacing(8)
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto))
            .RowDefinitions(new RowDefinition { Height = GridLength.Auto });
        _headerGrid.Children.Add(new StackPanel().Spacing(4).Children(
            new TextBlock().Style(AppTypography.Styles.HeadingText).FontSize(25).Text("Tổng quan kinh doanh")).Grid(row: 0, column: 0));
        _headerGrid.Children.Add(periodSelector.Grid(row: 0, column: 1));
        return _headerGrid;
    }

    private UIElement BuildPeriodSelector()
    {
        var segments = new Grid().ColumnSpacing(0).Width(252);
        for (var i = 0; i < _viewModel.Periods.Count; i++)
        {
            segments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var index = i;
            var button = new Button().Content(_viewModel.Periods[i].Label).MinWidth(74).MinHeight(38)
                .Padding(new Thickness(8, 4, 8, 4)).CornerRadius(new CornerRadius(7)).BorderThickness(new Thickness(0))
                .IsEnabled(x => x.Binding(() => _viewModel.CanChangePeriod));
            AutomationProperties.SetName(button, $"Kỳ thống kê: {_viewModel.Periods[i].Label}");
            button.Click += async (_, _) => await _viewModel.SelectPeriodCommand.ExecuteAsync(index);
            _periodButtons.Add(button);
            segments.Children.Add(button.Grid(column: i));
        }

        var segmentedFrame = new Border().BorderBrush(ThemeResource.Get<Brush>(AppColors.Stroke))
            .BorderThickness(new Thickness(1)).CornerRadius(new CornerRadius(9))
            .Background(ThemeResource.Get<Brush>(AppColors.Surface)).Padding(new Thickness(2)).Child(segments);
        var refresh = new Button().Width(40).Height(40).Padding(new Thickness(8))
            .Style(AppControlStyles.Styles.QuietButton)
            .Content(new FontIcon().Glyph("\uE72C").FontSize(15))
            .Command(x => x.Binding(() => _viewModel.RefreshCommand))
            .IsEnabled(x => x.Binding(() => _viewModel.CanRefresh));
        AutomationProperties.SetName(refresh, "Làm mới thống kê");
        ToolTipService.SetToolTip(refresh, "Làm mới kỳ đang chọn");

        UpdatePeriodSelector();
        return new StackPanel().Orientation(Orientation.Horizontal).Spacing(8)
            .VerticalAlignment(VerticalAlignment.Center).Children(segmentedFrame, refresh);
    }

    private void UpdatePeriodSelector()
    {
        for (var i = 0; i < _periodButtons.Count; i++)
        {
            var selected = i == _viewModel.SelectedPeriodIndex;
            var button = _periodButtons[i];
            button.Background(ThemeResource.Get<Brush>(selected ? AppColors.InfoFill : AppColors.Surface));
            button.Foreground(ThemeResource.Get<Brush>(selected ? AppColors.Primary : AppColors.SecondaryText));
            button.FontWeight(selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DashboardViewModel.SelectedPeriodIndex)) UpdatePeriodSelector();
        if (args.PropertyName == nameof(DashboardViewModel.Snapshot)) DrawRevenueChart();
    }

    private UIElement BuildMetrics()
    {
        var grid = _metricsGrid = new Grid().ColumnSpacing(12).RowSpacing(12);
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new StatCard().MinHeight(142).Title("Tổng doanh thu").ValueText(x => x.Binding(() => _viewModel.RevenueText))
            .UnitText("₫").TrendText(x => x.Binding(() => _viewModel.RevenueTrendText))
            .Tone(x => x.Binding(() => _viewModel.RevenueTrendText).Convert(GetTrendTone)).DetailText("so với kỳ trước")
            .IconSource(GetIcon(AppIcons.MoneyIcon)!).IsLoading(x => x.Binding(() => _viewModel.IsLoading)).Grid(column: 0));
        grid.Children.Add(new StatCard().MinHeight(142).Title("Số đơn hàng").ValueText(x => x.Binding(() => _viewModel.OrdersText))
            .UnitText("đơn").TrendText(x => x.Binding(() => _viewModel.OrdersTrendText))
            .Tone(x => x.Binding(() => _viewModel.OrdersTrendText).Convert(GetTrendTone)).DetailText("phát sinh trong kỳ")
            .IconSource(GetIcon(AppIcons.BagIcon)!).IsLoading(x => x.Binding(() => _viewModel.IsLoading)).Grid(column: 1));
        grid.Children.Add(new StatCard().MinHeight(142).Title("Sản phẩm đang hoạt động").ValueText(x => x.Binding(() => _viewModel.ProductsText))
            .UnitText("sản phẩm").DetailText("tại thời điểm đọc")
            .IconSource(GetIcon(AppIcons.BoxIcon)!).IsLoading(x => x.Binding(() => _viewModel.IsLoading)).Grid(column: 2));
        grid.Children.Add(new StatCard().MinHeight(142).Title("Cảnh báo tồn kho").ValueText(x => x.Binding(() => _viewModel.LowStockText))
            .UnitText("mặt hàng").TrendText(x => x.Binding(() => _viewModel.LowStockTrendText))
            .DetailText("theo ngưỡng tồn kho")
            .Tone(StatusTone.Warning).IconSource(GetIcon(AppIcons.WarningIcon)!)
            .IsLoading(x => x.Binding(() => _viewModel.IsLoading)).Grid(column: 3));
        return grid;
    }

    private UIElement BuildAnalyticsPanels()
    {
        _chartPanel = new Border().Style(AppControlStyles.Styles.Panel).Padding(20).Child(new StackPanel().Spacing(8).Children(
            new TextBlock().Style(AppTypography.Styles.SectionText).Text("Xu hướng doanh thu theo ngày"),
            new TextBlock().Style(AppTypography.Styles.CaptionText).Text(x => x.Binding(() => _viewModel.ChartPeakSummary)),
            _revenueCanvas,
            BuildDailyRevenueList()));

        _stockPanel = new Border().Style(AppControlStyles.Styles.Panel).Padding(20).Child(new StackPanel().Spacing(10).Children(
            new TextBlock().Style(AppTypography.Styles.SectionText).Text("Tồn kho thấp"),
            BuildLowStockList()));
        var recentPanel = new Border().Style(AppControlStyles.Styles.Panel).Padding(20).Child(new StackPanel().Spacing(10).Children(
            new Grid().ColumnDefinitions(new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)), new ColumnDefinition().Width(GridLength.Auto))
                .Children(new TextBlock().Style(AppTypography.Styles.SectionText).Text("5 đơn hàng phát sinh gần nhất").Grid(column: 0)),
            BuildRecentOrdersList()));

        _analyticsGrid = new Grid().ColumnSpacing(20).RowSpacing(20)
            .Visibility(x => x.Binding(() => _viewModel.ShowDashboardPanels).Convert(ShowIf));
        _analyticsGrid.Children.Add(_chartPanel.Grid(column: 0, row: 0));
        _analyticsGrid.Children.Add(_stockPanel.Grid(column: 1, row: 0));
        _analyticsGrid.Children.Add(recentPanel.Grid(column: 0, row: 1, columnSpan: 2));
        _analyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _analyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        UpdateAnalyticsLayout(1200);
        return _analyticsGrid;
    }

    private UIElement BuildDailyRevenueList()
    {
        var list = new ItemsControl().ItemsSource(x => x.Binding(() => _viewModel.DailyRevenueRows));
        list.ItemTemplate = new DataTemplate(() =>
            new Grid().ColumnDefinitions(new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)), new ColumnDefinition().Width(GridLength.Auto))
                .Children(BindText<DashboardDailyRevenueRow>(nameof(DashboardDailyRevenueRow.DateText)).Grid(column: 0),
                    BindText<DashboardDailyRevenueRow>(nameof(DashboardDailyRevenueRow.RevenueText)).Grid(column: 1)));
        AutomationProperties.SetName(list, "Doanh thu theo ngày, bảng số liệu truy cập được bằng bàn phím.");
        return new Expander { Header = "Bảng số liệu doanh thu theo ngày", Content = list };
    }

    private UIElement BuildLowStockList()
    {
        var list = new ItemsControl().ItemsSource(x => x.Binding(() => _viewModel.LowStockRows));
        list.ItemTemplate = new DataTemplate(BuildLowStockRow);
        return new StackPanel().Children(list,
            new TextBlock().Style(AppTypography.Styles.CaptionText).Text(_viewModel.LowStockEmptyText)
                .Visibility(x => x.Binding(() => _viewModel.LowStockIsEmpty).Convert(ShowIf)));
    }

    private static UIElement BuildLowStockRow()
    {
        var icon = new FontIcon().FontFamily(new FontFamily("Segoe Fluent Icons")).FontSize(15)
            .Foreground(ThemeResource.Get<Brush>(AppColors.Primary));
        icon.SetBinding(FontIcon.GlyphProperty, new Binding { Path = new PropertyPath(nameof(DashboardLowStockRow.IconGlyph)) });
        var product = BindText<DashboardLowStockRow>(nameof(DashboardLowStockRow.ProductName), AppTypography.BodyText);
        product.TextTrimming(TextTrimming.CharacterEllipsis).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold);
        var variant = BindText<DashboardLowStockRow>(nameof(DashboardLowStockRow.VariantName), AppTypography.CaptionText);
        variant.TextTrimming(TextTrimming.CharacterEllipsis).Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText));
        var available = BindText<DashboardLowStockRow>(nameof(DashboardLowStockRow.AvailableText), AppTypography.BodyText);
        available.FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
            .Foreground(ThemeResource.Get<Brush>(AppColors.DangerText)).HorizontalAlignment(HorizontalAlignment.Right);
        var unit = new TextBlock().Text("máy").Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(ThemeResource.Get<Brush>(AppColors.DangerText));
        return new Border().BorderBrush(ThemeResource.Get<Brush>(AppColors.Stroke))
            .BorderThickness(new Thickness(0, 0, 0, 1)).Padding(new Thickness(0, 9, 0, 9))
            .Child(new Grid().ColumnSpacing(10).ColumnDefinitions(
                    new ColumnDefinition().Width(GridLength.Auto),
                    new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition().Width(GridLength.Auto))
                .Children(
                    new Border().Width(34).Height(34).CornerRadius(new CornerRadius(7))
                        .Background(ThemeResource.Get<Brush>(AppColors.InfoFill))
                        .Child(icon.HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center)).Grid(column: 0),
                    new StackPanel().Spacing(2).Children(product, variant).Grid(column: 1),
                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(4)
                        .VerticalAlignment(VerticalAlignment.Center).Children(available, unit).Grid(column: 2)));
    }

    private UIElement BuildRecentOrdersList()
    {
        var list = new ItemsControl().HorizontalAlignment(HorizontalAlignment.Stretch)
            .ItemsSource(x => x.Binding(() => _viewModel.RecentOrderRows));
        list.ItemTemplate = new DataTemplate(BuildRecentOrderRow);
        AutomationProperties.SetName(list, "Năm đơn hàng gần nhất");
        var table = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch }.RowSpacing(0);
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        table.Children.Add(BuildRecentOrderHeader().Grid(row: 0, column: 0));
        table.Children.Add(list.Grid(row: 1, column: 0));
        var empty = new TextBlock().Style(AppTypography.Styles.CaptionText).Text(_viewModel.RecentOrdersEmptyText)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Visibility(x => x.Binding(() => _viewModel.RecentOrdersAreEmpty).Convert(ShowIf));
        return new StackPanel().Children(table, empty);
    }

    private static UIElement BuildRecentOrderHeader()
    {
        var header = new Grid().ColumnSpacing(12).Padding(new Thickness(12, 10, 12, 10));
        AddOrderColumns(header);
        header.Children.Add(HeaderText("MÃ ĐƠN").Grid(column: 0));
        header.Children.Add(HeaderText("KHÁCH HÀNG").Grid(column: 1));
        header.Children.Add(HeaderText("SẢN PHẨM").Grid(column: 2));
        header.Children.Add(HeaderText("TỔNG TIỀN").HorizontalAlignment(HorizontalAlignment.Right).Grid(column: 3));
        header.Children.Add(HeaderText("TRẠNG THÁI").HorizontalAlignment(HorizontalAlignment.Center).Grid(column: 4));
        return new Border().HorizontalAlignment(HorizontalAlignment.Stretch)
            .BorderBrush(ThemeResource.Get<Brush>(AppColors.Stroke))
            .BorderThickness(new Thickness(0, 0, 0, 1)).Child(header);
    }

    private static UIElement BuildRecentOrderRow()
    {
        var row = new Grid().ColumnSpacing(12).Padding(new Thickness(12, 10, 12, 10));
        AddOrderColumns(row);
        var code = BindText<DashboardRecentOrderRow>(nameof(DashboardRecentOrderRow.OrderCode), AppTypography.BodyText);
        code.Foreground(ThemeResource.Get<Brush>(AppColors.Primary)).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
            .TextTrimming(TextTrimming.CharacterEllipsis);
        var customer = new StackPanel().Spacing(2).Children(
            BindText<DashboardRecentOrderRow>(nameof(DashboardRecentOrderRow.CustomerDisplayName), AppTypography.BodyText),
            BindText<DashboardRecentOrderRow>(nameof(DashboardRecentOrderRow.CreatedAtText), AppTypography.CaptionText)
                .Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText)));
        var product = BindText<DashboardRecentOrderRow>(nameof(DashboardRecentOrderRow.ProductSummary), AppTypography.BodyText);
        product.TextTrimming(TextTrimming.CharacterEllipsis);
        var amount = BindText<DashboardRecentOrderRow>(nameof(DashboardRecentOrderRow.TotalText), AppTypography.BodyText);
        amount.HorizontalAlignment(HorizontalAlignment.Right).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold);
        var status = new StatusBadge();
        status.SetBinding(StatusBadge.LabelProperty, new Binding { Path = new PropertyPath(nameof(DashboardRecentOrderRow.StatusLabel)) });
        status.SetBinding(StatusBadge.ToneProperty, new Binding
        {
            Path = new PropertyPath(nameof(DashboardRecentOrderRow.Status)),
            Converter = DashboardStatusToneConverter.Instance
        });
        row.Children.Add(code.Grid(column: 0));
        row.Children.Add(customer.Grid(column: 1));
        row.Children.Add(product.Grid(column: 2));
        row.Children.Add(amount.Grid(column: 3));
        row.Children.Add(status.HorizontalAlignment(HorizontalAlignment.Center).Grid(column: 4));
        return new Border().HorizontalAlignment(HorizontalAlignment.Stretch)
            .BorderBrush(ThemeResource.Get<Brush>(AppColors.Stroke))
            .BorderThickness(new Thickness(0, 0, 0, 1)).Child(row);
    }

    private static void AddOrderColumns(Grid grid)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(43, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(13, GridUnitType.Star) });
    }

    private static TextBlock HeaderText(string text) => new TextBlock().Text(text)
        .FontSize(11).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
        .Foreground(ThemeResource.Get<Brush>(AppColors.SecondaryText));

    private void DrawRevenueChart()
    {
        foreach (var element in _revenueCanvas.Children.Where(element => !ReferenceEquals(element, _revenueLine)).ToArray())
            _revenueCanvas.Children.Remove(element);
        var points = _viewModel.Snapshot?.DailyRevenue ?? [];
        const double left = 48;
        const double right = 14;
        const double top = 20;
        const double bottom = 34;
        var width = _revenueCanvas.ActualWidth;
        var height = _revenueCanvas.ActualHeight;
        var layout = RevenueChartLayout.Calculate(points, width, height, left, right, top, bottom);
        if (layout.Points.Count == 0) return;

        var plotHeight = height - top - bottom;
        var plotWidth = width - left - right;
        var axisBrush = ThemeResource.Get<Brush>(AppColors.Stroke);
        var textBrush = ThemeResource.Get<Brush>(AppColors.SecondaryText);
        var primaryBrush = ThemeResource.Get<Brush>(AppColors.Primary);
        var surfaceBrush = ThemeResource.Get<Brush>(AppColors.Surface);
        var barBrush = CreateRevenueBarBrush();
        for (var tick = 0; tick <= 4; tick++)
        {
            var value = layout.AxisMaximum - tick * layout.AxisInterval;
            var y = top + plotHeight * tick / 4d;
            var gridLine = new Line { X1 = left, X2 = width - right, Y1 = y, Y2 = y, StrokeThickness = 1, Opacity = 0.65 };
            gridLine.Stroke(axisBrush);
            _revenueCanvas.Children.Add(gridLine);
            var label = new TextBlock().Text(FormatAxisValue(value)).FontSize(10).Foreground(textBrush)
                .TextAlignment(TextAlignment.Right).Width(left - 8);
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 8);
            _revenueCanvas.Children.Add(label);
        }

        var slotWidth = plotWidth / points.Count;
        var barWidth = Math.Min(40, slotWidth * 0.54);
        var peakIndex = points.Select((point, index) => (point, index)).MaxBy(pair => pair.point.Revenue).index;
        var baseline = top + plotHeight;
        var area = new Polygon { Opacity = 0.10 };
        area.Fill(primaryBrush);
        area.Points.Add(new Windows.Foundation.Point(layout.Points[0].X, baseline));
        foreach (var point in layout.Points) area.Points.Add(new Windows.Foundation.Point(point.X, point.Y));
        area.Points.Add(new Windows.Foundation.Point(layout.Points[^1].X, baseline));
        _revenueCanvas.Children.Add(area);

        for (var index = 0; index < points.Count; index++)
        {
            var point = layout.Points[index];
            var barHeight = Math.Max(2, baseline - point.Y);
            var bar = new Rectangle { Width = barWidth, Height = barHeight, Opacity = index == peakIndex ? 1 : 0.82 };
            bar.Fill(barBrush);
            Canvas.SetLeft(bar, point.X - barWidth / 2);
            Canvas.SetTop(bar, baseline - barHeight);
            _revenueCanvas.Children.Add(bar);

            var dayLabel = new TextBlock().Text(GetChartDateLabel(points[index].Date, points.Count))
                .FontSize(10).TextAlignment(TextAlignment.Center).Width(slotWidth).Foreground(textBrush);
            if (index == peakIndex)
            {
                dayLabel.Foreground(primaryBrush);
                dayLabel.FontWeight(Microsoft.UI.Text.FontWeights.Bold);
            }
            Canvas.SetLeft(dayLabel, left + index * slotWidth);
            Canvas.SetTop(dayLabel, baseline + 7);
            _revenueCanvas.Children.Add(dayLabel);
        }

        var trendFigure = new PathFigure { StartPoint = new Windows.Foundation.Point(layout.Points[0].X, layout.Points[0].Y) };
        for (var index = 1; index < layout.Points.Count; index++)
        {
            var previous = layout.Points[index - 1];
            var current = layout.Points[index];
            var third = (current.X - previous.X) / 3;
            trendFigure.Segments.Add(new BezierSegment
            {
                Point1 = new Windows.Foundation.Point(previous.X + third, previous.Y),
                Point2 = new Windows.Foundation.Point(current.X - third, current.Y),
                Point3 = new Windows.Foundation.Point(current.X, current.Y)
            });
        }
        var trendGeometry = new PathGeometry();
        trendGeometry.Figures.Add(trendFigure);
        _revenueLine.Data = trendGeometry;
        _revenueLine.Stroke(primaryBrush);
        Canvas.SetZIndex(_revenueLine, 10);
        for (var index = 0; index < layout.Points.Count; index++)
        {
            var point = layout.Points[index];
            var marker = new Ellipse
            {
                Width = index == peakIndex ? 10 : 8,
                Height = index == peakIndex ? 10 : 8
            };
            marker.Fill(surfaceBrush);
            marker.Stroke(primaryBrush);
            marker.StrokeThickness = index == peakIndex ? 3 : 2;
            Canvas.SetLeft(marker, point.X - marker.Width / 2);
            Canvas.SetTop(marker, point.Y - marker.Height / 2);
            Canvas.SetZIndex(marker, 11);
            _revenueCanvas.Children.Add(marker);
        }

        var peakPoint = layout.Points[peakIndex];
        var peakLabel = new Border().Background(ThemeResource.Get<Brush>(AppColors.Text))
            .CornerRadius(new CornerRadius(4)).Padding(new Thickness(6, 3, 6, 3))
            .Child(new TextBlock().Text(FormatCurrency(points[peakIndex].Revenue)).FontSize(10)
                .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                .Foreground(ThemeResource.Get<Brush>(AppColors.Surface)));
        Canvas.SetLeft(peakLabel, Math.Clamp(peakPoint.X - 38, left, Math.Max(left, width - right - 82)));
        Canvas.SetTop(peakLabel, Math.Max(0, peakPoint.Y - 27));
        Canvas.SetZIndex(peakLabel, 12);
        _revenueCanvas.Children.Add(peakLabel);
    }

    private static string GetChartDateLabel(DateOnly date, int count) => count switch
    {
        1 => date.ToString("dd/MM"),
        7 => date.DayOfWeek switch
        {
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            DayOfWeek.Sunday => "CN",
            _ => string.Empty
        },
        _ => date.ToString("dd")
    };

    private static string FormatAxisValue(decimal value) => value >= 1_000_000m
        ? $"{value / 1_000_000m:0.#}M"
        : value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    private static string FormatCurrency(decimal value) =>
        $"{value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} ₫";

    private static LinearGradientBrush CreateRevenueBarBrush()
    {
        var color = Application.Current.Resources.TryGetValue(AppColors.Primary, out var value) && value is SolidColorBrush brush
            ? brush.Color
            : StitchTokens.Primary;
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1)
        };
        gradient.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(0x75, color.R, color.G, color.B), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(0xF0, color.R, color.G, color.B), Offset = 1 });
        return gradient;
    }

    private static StatusTone GetTrendTone(string value) => value.TrimStart().StartsWith("-", StringComparison.Ordinal)
        ? StatusTone.Danger
        : StatusTone.Success;

    private void UpdateMetricsLayout(double width)
    {
        if (_metricsGrid is null) return;
        var columns = width < 680 ? 1 : width < 960 ? 2 : 4;
        for (var i = 0; i < 4; i++)
        {
            var card = _metricsGrid.Children[i];
            Grid.SetColumn(card, i % columns);
            Grid.SetRow(card, i / columns);
        }
        _metricsGrid.ColumnDefinitions.Clear();
        for (var i = 0; i < columns; i++)
            _metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    private void UpdateAnalyticsLayout(double width)
    {
        if (_analyticsGrid is null || _chartPanel is null || _stockPanel is null) return;
        var twoColumns = width >= 900;
        _analyticsGrid.ColumnDefinitions.Clear();
        if (twoColumns)
        {
            _analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star) });
            _analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            Grid.SetRow(_chartPanel, 0); Grid.SetColumn(_chartPanel, 0);
            Grid.SetRow(_stockPanel, 0); Grid.SetColumn(_stockPanel, 1);
            Grid.SetRow(_analyticsGrid.Children[2], 1); Grid.SetColumn(_analyticsGrid.Children[2], 0);
            Grid.SetColumnSpan(_analyticsGrid.Children[2], 2);
            _analyticsGrid.RowDefinitions.Clear();
            _analyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _analyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        else
        {
            _analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_chartPanel, 0); Grid.SetColumn(_chartPanel, 0);
            Grid.SetRow(_stockPanel, 1); Grid.SetColumn(_stockPanel, 0);
            Grid.SetRow(_analyticsGrid.Children[2], 2); Grid.SetColumn(_analyticsGrid.Children[2], 0);
            Grid.SetColumnSpan(_analyticsGrid.Children[2], 1);
            _analyticsGrid.RowDefinitions.Clear();
            for (var i = 0; i < 3; i++) _analyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private void UpdateHeaderLayout(double width)
    {
        if (_headerGrid is null || _headerGrid.Children.Count < 2) return;
        var stacked = width < 820;
        _headerGrid.ColumnDefinitions.Clear();
        _headerGrid.RowDefinitions.Clear();
        if (stacked)
        {
            _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_headerGrid.Children[0], 0); Grid.SetColumn(_headerGrid.Children[0], 0);
            Grid.SetRow(_headerGrid.Children[1], 1); Grid.SetColumn(_headerGrid.Children[1], 0);
            ((FrameworkElement)_headerGrid.Children[1]).HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_headerGrid.Children[0], 0); Grid.SetColumn(_headerGrid.Children[0], 0);
            Grid.SetRow(_headerGrid.Children[1], 0); Grid.SetColumn(_headerGrid.Children[1], 1);
            ((FrameworkElement)_headerGrid.Children[1]).HorizontalAlignment = HorizontalAlignment.Right;
        }
    }

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    private static IconSource? GetIcon(string key) => Application.Current.Resources.TryGetValue(key, out var value) ? value as IconSource : null;

    private static TextBlock BindText<T>(string path, string? style = null)
    {
        var text = new TextBlock();
        if (style is not null) text.Style(AppResources.GetStyle(style));
        text.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(path), Mode = BindingMode.OneWay });
        return text;
    }

    private sealed class DashboardStatusToneConverter : IValueConverter
    {
        public static DashboardStatusToneConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, string language) => value is TechStore.Shared.Enums.OrderStatus status
            ? status switch
            {
                TechStore.Shared.Enums.OrderStatus.Completed => StatusTone.Success,
                TechStore.Shared.Enums.OrderStatus.Processing => StatusTone.Info,
                TechStore.Shared.Enums.OrderStatus.Pending => StatusTone.Warning,
                TechStore.Shared.Enums.OrderStatus.Cancelled or TechStore.Shared.Enums.OrderStatus.Refunded or TechStore.Shared.Enums.OrderStatus.PartiallyRefunded => StatusTone.Danger,
                _ => StatusTone.Neutral
            }
            : StatusTone.Neutral;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
