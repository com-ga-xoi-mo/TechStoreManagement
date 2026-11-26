using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Shared;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Dashboard;

internal sealed class FinancialReportView(FinancialReportViewModel report)
{
    private SegmentedTabButton? _overviewTab;
    private SegmentedTabButton? _financialTab;
    private Button? _reload;

    /// <summary>Starts moving focus when the ViewModel asks for it (focus cannot be bound).</summary>
    public void Attach()
    {
        report.FocusRequested -= OnFocusRequested;
        report.FocusRequested += OnFocusRequested;
    }

    public void Detach() => report.FocusRequested -= OnFocusRequested;

    private void OnFocusRequested(object? sender, FinancialReportFocusTarget target)
    {
        Control? element = target == FinancialReportFocusTarget.Reload
            ? _reload
            : report.IsOpen ? _financialTab : _overviewTab;
        element?.DispatcherQueue.TryEnqueue(() =>
        {
            if (element.XamlRoot is not null) element.Focus(FocusState.Programmatic);
        });
    }

    public FrameworkElement BuildTabs()
    {
        var overview = new SegmentedTabButton("Tổng quan", "DashboardOverviewTab")
            .Command(x => x.Binding(() => report.CloseCommand))
            .IsSelected(x => x.Binding(() => report.ShowOverview));
        var financial = new SegmentedTabButton("Báo cáo tài chính", "DashboardFinancialReportTab")
            .Command(x => x.Binding(() => report.OpenCommand))
            .IsSelected(x => x.Binding(() => report.IsOpen));
        _overviewTab = overview;
        _financialTab = financial;

        var tabStrip = new Border()
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(4))
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Margin(new Thickness(20, 14, 20, 0))
            .DataContext(report)
            .Visibility(x => x.Binding(() => report.IsAvailable).Convert(ShowIf))
            .Child(new StackPanel().Orientation(Orientation.Horizontal).Spacing(4).Children(overview, financial));
        AutomationProperties.SetName(tabStrip, "Chế độ xem Tổng quan");
        return tabStrip;
    }

    public ScrollViewer BuildContent()
    {
        var header = new StackPanel().Spacing(4).Children(
            new TextBlock().Style(AppTypography.Styles.HeadingText).FontSize(25).Foreground(AppColors.Brushes.Text)
                .Text("Báo cáo tài chính"),
            Caption().Text("Theo dõi doanh thu thuần, dòng tiền, giá vốn, lãi gộp và hiệu suất theo kỳ."));

        var filter = BuildFilter();

        var spinner = new ProgressRing().Width(18).Height(18)
            .IsActive(x => x.Binding(() => report.IsLoading));
        var loadingText = Label().Text("Đang tải báo cáo…");
        AutomationProperties.SetLiveSetting(loadingText, AutomationLiveSetting.Polite);
        var loading = Panel().Padding(new Thickness(16, 12, 16, 12))
            .Visibility(x => x.Binding(() => report.IsLoading).Convert(ShowIf))
            .Child(new StackPanel().Orientation(Orientation.Horizontal).Spacing(10)
                .Children(spinner, loadingText.VerticalAlignment(VerticalAlignment.Center)));

        var error = new InfoBar().IsOpen(x => x.Binding(() => report.IsError)).IsClosable(false)
            .Severity(InfoBarSeverity.Error).Title("Không tải được báo cáo")
            .Message(x => x.Binding(() => report.ErrorMessage));

        var emptyNotice = new Border()
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(AppMetrics.Values.CardRadius)
            .Padding(new Thickness(14, 10, 14, 10))
            .Visibility(x => x.Binding(() => report.IsEmpty).Convert(ShowIf))
            .Child(Caption().Text("Không có số liệu phát sinh khác 0 trong kỳ."));

        var allocationNotice = new Border()
            .Background(AppColors.Brushes.WarningFill)
            .BorderBrush(AppColors.Brushes.WarningStroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(AppMetrics.Values.CardRadius)
            .Padding(new Thickness(14, 10, 14, 10))
            .Visibility(x => x.Binding(() => report.Presentation!.AllocationNotice).Convert(ShowIfHasText))
            .Child(new TextBlock().Style(AppResources.GetStyle(AppTypography.CaptionText))
                .Foreground(AppColors.Brushes.WarningText)
                .TextWrapping(TextWrapping.Wrap)
                .Text(x => x.Binding(() => report.Presentation!.AllocationNotice)));

        var kpiGrid = BuildKpiGrid();
        var chartRow = BuildChartRow();
        var stepGrid = BuildStepGrid();
        var supportGrid = new Grid().ColumnSpacing(12).RowSpacing(12).Children(
            CardHost().Content(x => x.Binding(() => report.Presentation!.OrdersTaxSection)).ContentTemplate<FinancialReportSection>(SectionCard),
            CardHost().Content(x => x.Binding(() => report.Presentation!.CostSupportSection)).ContentTemplate<FinancialReportSection>(SectionCard));
        UpdateTwoColumnGrid(supportGrid, 1000);

        var dayHeader = BuildDayColumnHeader();
        var dailyList = new ItemsControl().ItemsSource(x => x.Binding(() => report.Presentation!.Daily))
            .ItemTemplate<FinancialReportDayRow>(BuildDay);
        var listsGrid = BuildFourCardGrid(
            CardHost().Content(x => x.Binding(() => report.Presentation!.RankedQuantityList)).ContentTemplate<FinancialReportRankedList>(RankedCard),
            CardHost().Content(x => x.Binding(() => report.Presentation!.RankedRevenueList)).ContentTemplate<FinancialReportRankedList>(RankedCard),
            CardHost().Content(x => x.Binding(() => report.Presentation!.RankedCashierList)).ContentTemplate<FinancialReportRankedList>(RankedCard),
            CardHost().Content(x => x.Binding(() => report.Presentation!.RankedShiftList)).ContentTemplate<FinancialReportRankedList>(RankedCard));

        var snapshot = new StackPanel().Spacing(20)
            .Visibility(x => x.Binding(() => report.HasSnapshot).Convert(ShowIf))
            .Children(
                emptyNotice,
                allocationNotice,
                kpiGrid,
                chartRow,
                SectionHeading("Tổng kỳ chi tiết", "Các chỉ số tài chính tổng hợp do máy chủ tính cho toàn bộ kỳ."),
                new StackPanel().Spacing(12).Children(stepGrid, supportGrid),
                SectionHeading("Theo ngày", "Chọn từng ngày để mở đầy đủ chỉ số bán, hoàn, dòng tiền và giá vốn."),
                new StackPanel().Spacing(6).Children(dayHeader, dailyList),
                SectionHeading("Xếp hạng & Ca tiền mặt", "Top sản phẩm theo số lượng, doanh thu, hiệu suất thu ngân và ca tiền mặt."),
                listsGrid);

        var body = new StackPanel().Padding(new Thickness(20, 16, 20, 24)).Spacing(16)
            .Children(header, filter, loading, error, snapshot);
        body.SizeChanged += (_, args) =>
        {
            var width = args.NewSize.Width;
            UpdateKpiLayout(kpiGrid, width);
            UpdateChartRow(chartRow, width);
            UpdateStepLayout(stepGrid, width);
            UpdateTwoColumnGrid(supportGrid, width);
            UpdateTwoColumnGrid(listsGrid, width);
            dayHeader.Visibility = width >= DayHeaderMinWidth ? Visibility.Visible : Visibility.Collapsed;
        };

        return new ScrollViewer().DataContext(report).VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .Content(body);
    }

    // The column header of the daily list is shown only while the rows use their wide, column layout.
    private const double DayHeaderMinWidth = 700;
    private const double DayRowMinWidth = 540;

    private Border BuildFilter()
    {
        var today = new SegmentedTabButton("Hôm nay", "FinancialReportPresetToday")
            .Command(x => x.Binding(() => report.TodayCommand))
            .IsSelected(x => x.Binding(() => report.IsTodayPreset));
        var last7Days = new SegmentedTabButton("7 ngày qua", "FinancialReportPresetLast7Days")
            .Command(x => x.Binding(() => report.Last7DaysCommand))
            .IsSelected(x => x.Binding(() => report.IsLast7DaysPreset));
        var thisMonth = new SegmentedTabButton("Tháng này", "FinancialReportPresetThisMonth")
            .Command(x => x.Binding(() => report.ThisMonthCommand))
            .IsSelected(x => x.Binding(() => report.IsThisMonthPreset));
        var presets = new StackPanel().Orientation(Orientation.Horizontal).Spacing(12)
            .Children(SegmentStrip("Chọn nhanh khoảng thời gian", today, last7Days, thisMonth),
                Caption().VerticalAlignment(VerticalAlignment.Center).Text("Chọn nhanh sẽ tải lại báo cáo ngay."));

        var start = DateInput("Từ ngày", "FinancialReportFromDate")
            .Text(x => x.Binding(() => report.From).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        var end = DateInput("Đến ngày", "FinancialReportToDate")
            .Text(x => x.Binding(() => report.To).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        var startError = DateError().Text(x => x.Binding(() => report.FromError))
            .Visibility(x => x.Binding(() => report.FromError).Convert(ShowIfHasText));
        var endError = DateError().Text(x => x.Binding(() => report.ToError))
            .Visibility(x => x.Binding(() => report.ToError).Convert(ShowIfHasText));
        var startField = new StackPanel().Spacing(4).Children(start, startError);
        var endField = new StackPanel().Spacing(4).Children(end, endError);

        var apply = new Button().Style(AppControlStyles.Styles.PrimaryButton).MinHeight(36)
            .Padding(new Thickness(16, 0, 16, 0)).Content("Áp dụng")
            .Command(x => x.Binding(() => report.ApplyCommand));
        var reload = new Button().Style(AppControlStyles.Styles.QuietButton).MinHeight(36)
            .Padding(new Thickness(14, 0, 14, 0))
            .Content(x => x.Binding(() => report.ReloadLabel))
            .Command(x => x.Binding(() => report.ReloadCommand));
        _reload = reload;
        AutomationProperties.SetAutomationId(apply, "FinancialReportApply");
        AutomationProperties.SetAutomationId(reload, "FinancialReportReload");

        var buttons = new StackPanel().Orientation(Orientation.Horizontal).Spacing(8)
            .VerticalAlignment(VerticalAlignment.Top).Children(apply, reload);

        var reference = Label().FontSize(12).FontWeight(FontWeights.SemiBold)
            .Text(x => x.Binding(() => report.RangeLabel))
            .Visibility(x => x.Binding(() => report.RangeLabel).Convert(ShowIfHasText));
        var limit = Caption().Text($"Tối đa {AnalyticsLimits.MaxCustomRangeDays} ngày theo giờ Việt Nam (UTC+07).");
        var meta = new StackPanel().Spacing(2).VerticalAlignment(VerticalAlignment.Center)
            .Children(reference, limit);

        var grid = new Grid().ColumnSpacing(12).RowSpacing(10).Children(startField, endField, buttons, meta);

        void ApplyFilterLayout(double width)
        {
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            if (width >= 820)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(172) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(172) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                SetCell(startField, 0, 0);
                SetCell(endField, 0, 1);
                SetCell(buttons, 0, 2);
                SetCell(meta, 0, 3);
                buttons.Margin = new Thickness(0, 25, 0, 0);
                meta.HorizontalAlignment = HorizontalAlignment.Right;
                reference.TextAlignment = TextAlignment.Right;
                limit.TextAlignment = TextAlignment.Right;
            }
            else if (width >= 480)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                SetCell(startField, 0, 0);
                SetCell(endField, 0, 1);
                SetCell(buttons, 1, 0);
                SetCell(meta, 1, 1);
                buttons.Margin = new Thickness(0);
                meta.HorizontalAlignment = HorizontalAlignment.Right;
                reference.TextAlignment = TextAlignment.Right;
                limit.TextAlignment = TextAlignment.Right;
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (var i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                SetCell(startField, 0, 0);
                SetCell(endField, 1, 0);
                SetCell(buttons, 2, 0);
                SetCell(meta, 3, 0);
                buttons.Margin = new Thickness(0);
                meta.HorizontalAlignment = HorizontalAlignment.Left;
                reference.TextAlignment = TextAlignment.Left;
                limit.TextAlignment = TextAlignment.Left;
            }
        }

        ApplyFilterLayout(1000);
        grid.SizeChanged += (_, args) => ApplyFilterLayout(args.NewSize.Width);
        return Panel(panelRadius: true).Padding(16).Child(new StackPanel().Spacing(14).Children(presets, grid));
    }

    /// <summary>Segmented strip with the same look as the Overview / Financial report selector.</summary>
    private static Border SegmentStrip(string automationName, params SegmentedTabButton[] buttons)
    {
        var strip = new Border()
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(4))
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Child(new StackPanel().Orientation(Orientation.Horizontal).Spacing(4).Children(buttons));
        AutomationProperties.SetName(strip, automationName);
        return strip;
    }

    private Grid BuildKpiGrid()
    {
        var grid = new Grid().ColumnSpacing(12).RowSpacing(12).Children(
            CardHost().Content(x => x.Binding(() => report.Presentation!.NetSalesHeadline)).ContentTemplate<FinancialReportHeadlineTile>(tile => HeadlineTile(tile, AppColors.Brushes.Primary)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.GrossProfitHeadline)).ContentTemplate<FinancialReportHeadlineTile>(tile => HeadlineTile(tile, AppColors.Brushes.SuccessText)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.ReceivedHeadline)).ContentTemplate<FinancialReportHeadlineTile>(tile => HeadlineTile(tile, AppColors.Brushes.Text)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.WarrantyHeadline)).ContentTemplate<FinancialReportHeadlineTile>(tile => HeadlineTile(tile, AppColors.Brushes.WarningText)));
        UpdateKpiLayout(grid, 1000);
        return grid;
    }

    private static void UpdateKpiLayout(Grid grid, double width) =>
        LayoutCells(grid, width >= 960 ? 4 : width >= 500 ? 2 : 1);

    private static void UpdateStepLayout(Grid grid, double width) =>
        LayoutCells(grid, width >= 1100 ? 6 : width >= 700 ? 3 : width >= 420 ? 2 : 1);

    private static void UpdateTwoColumnGrid(Grid grid, double width) => LayoutCells(grid, width >= 700 ? 2 : 1);

    private static void UpdateChartRow(Grid grid, double width)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (width >= 900)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SetCell(grid.Children[0], 0, 0);
            SetCell(grid.Children[1], 0, 1);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < grid.Children.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SetCell(grid.Children[i], i, 0);
            }
        }
    }

    // Places the children of the grid row by row in equally wide columns.
    private static void LayoutCells(Grid grid, int columns)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < (grid.Children.Count + columns - 1) / columns; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < grid.Children.Count; i++) SetCell(grid.Children[i], i / columns, i % columns);
    }

    private static Grid BuildFourCardGrid(FrameworkElement a, FrameworkElement b, FrameworkElement c, FrameworkElement d)
    {
        var grid = new Grid().ColumnSpacing(12).RowSpacing(12).Children(a, b, c, d);
        UpdateTwoColumnGrid(grid, 1000);
        return grid;
    }

    private Grid BuildStepGrid()
    {
        var grid = new Grid().ColumnSpacing(8).RowSpacing(8).Children(
            CardHost().Content(x => x.Binding(() => report.Presentation!.SalesStep)).ContentTemplate<FinancialReportStep>(step => StepTile(step, AppColors.Brushes.Text)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.RefundStep)).ContentTemplate<FinancialReportStep>(step => StepTile(step, AppColors.Brushes.Text)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.NetSalesStep)).ContentTemplate<FinancialReportStep>(step => StepTile(step, AppColors.Brushes.Primary)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.CostStep)).ContentTemplate<FinancialReportStep>(step => StepTile(step, AppColors.Brushes.Text)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.GrossProfitStep)).ContentTemplate<FinancialReportStep>(step => StepTile(step, AppColors.Brushes.SuccessText)),
            CardHost().Content(x => x.Binding(() => report.Presentation!.WarrantyStep)).ContentTemplate<FinancialReportStep>(step => StepTile(step, AppColors.Brushes.WarningText)));
        UpdateStepLayout(grid, 1000);
        return grid;
    }

    private Border BuildTrendCard()
    {
        var sales = new SegmentedTabButton("Doanh thu & lãi", "FinancialChartSalesMode")
            .Command(x => x.Binding(() => report.ShowSalesChartCommand))
            .IsSelected(x => x.Binding(() => report.IsSalesChart));
        var cashFlow = new SegmentedTabButton("Dòng tiền", "FinancialChartCashFlowMode")
            .Command(x => x.Binding(() => report.ShowCashFlowChartCommand))
            .IsSelected(x => x.Binding(() => report.IsCashFlowChart));

        var title = new StackPanel().Spacing(2).VerticalAlignment(VerticalAlignment.Center).Children(
            Label().FontSize(16).FontWeight(FontWeights.SemiBold).Text("Xu hướng theo ngày"),
            Caption().Text(x => x.Binding(() => report.IsSalesChart).Convert((bool isSales) => isSales
                ? "Hàng bán, hàng hoàn chưa thuế và lãi gộp sau hoàn của từng ngày."
                : "Tiền mặt, VietQR đã thu và tiền đã chi hoàn của từng ngày.")));
        var head = new Grid().ColumnDefinitions("*, Auto").ColumnSpacing(12).Children(
            title,
            SegmentStrip("Chọn dữ liệu biểu đồ", sales, cashFlow).VerticalAlignment(VerticalAlignment.Top).Grid(column: 1));

        var chart = new FinancialTrendChart()
            .Days(x => x.Binding(() => report.Presentation!.ChartDays))
            .Mode(x => x.Binding(() => report.ChartMode));
        AutomationProperties.SetName(chart, "Biểu đồ xu hướng theo ngày");
        AutomationProperties.SetAutomationId(chart, "FinancialReportTrendChart");

        return Panel(panelRadius: true).Padding(16).Child(new StackPanel().Spacing(12).Children(head, chart));
    }

    private Border BuildChannelCard()
    {
        var donut = new FinancialDonutChart().Channels(x => x.Binding(() => report.Presentation!.Channels));

        var legend = new StackPanel().Orientation(Orientation.Horizontal).Spacing(16).HorizontalAlignment(HorizontalAlignment.Center)
            .Children(
                LegendEntry(AppColors.Brushes.SuccessText, x => x.Binding(() => report.Presentation!.Channels.CashShareText)),
                LegendEntry(AppColors.Brushes.AccentText, x => x.Binding(() => report.Presentation!.Channels.QrShareText)));

        var unassigned = new Border()
            .Background(AppColors.Brushes.WarningFill)
            .BorderBrush(AppColors.Brushes.WarningStroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(AppMetrics.Values.CardRadius)
            .Padding(new Thickness(10, 8, 10, 8))
            .Visibility(x => x.Binding(() => report.Presentation!.Channels.UnassignedNote).Convert(ShowIfHasText))
            .Child(new TextBlock().Style(AppResources.GetStyle(AppTypography.CaptionText))
                .Foreground(AppColors.Brushes.WarningText)
                .TextWrapping(TextWrapping.Wrap)
                .Text(x => x.Binding(() => report.Presentation!.Channels.UnassignedNote)));

        var figures = new ItemsControl()
            .ItemsSource(x => x.Binding(() => report.Presentation!.Channels.Figures))
            .ItemTemplate<FinancialReportValue>(ValueRow);

        return Panel(panelRadius: true).Padding(16).Child(new StackPanel().Spacing(12).Children(
            new StackPanel().Spacing(2).Children(
                Label().FontSize(16).FontWeight(FontWeights.SemiBold).Text("Kênh thu tiền"),
                Caption().Text("Tỷ trọng tiền mặt và VietQR chỉ để so sánh, không thay đổi số liệu.")),
            donut,
            legend,
            unassigned,
            figures));
    }

    private static StackPanel LegendEntry(Action<IDependencyPropertyBuilder<Brush>> color, Action<IDependencyPropertyBuilder<string>> text)
    {
        var dot = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
        dot.Fill(color);
        return new StackPanel().Orientation(Orientation.Horizontal).Spacing(6).Children(dot, Label().FontSize(12).Text(text));
    }

    private Grid BuildChartRow()
    {
        var grid = new Grid().ColumnSpacing(12).RowSpacing(12).Children(BuildTrendCard(), BuildChannelCard());
        UpdateChartRow(grid, 1000);
        return grid;
    }

    // Each call site binds the card content with its own inline lambda; the template parameters below are
    // null while the tree is built and are only used inside binding lambdas.
    private static ContentControl CardHost() => new ContentControl()
        .HorizontalContentAlignment(HorizontalAlignment.Stretch)
        .VerticalContentAlignment(VerticalAlignment.Stretch);

    private static Border HeadlineTile(FinancialReportHeadlineTile tile, Action<IDependencyPropertyBuilder<Brush>> valueBrush)
    {
        return Panel(panelRadius: true).Padding(16).Child(new StackPanel().Spacing(6).Children(
            Caption().Text(x => x.Binding(() => tile.Title)),
            new TextBlock().Style(AppTypography.Styles.HeadingText).FontSize(22).Foreground(valueBrush).TextWrapping(TextWrapping.Wrap)
                .Text(x => x.Binding(() => tile.Value)),
            Caption().Text(x => x.Binding(() => tile.Detail))));
    }

    private static Border StepTile(FinancialReportStep step, Action<IDependencyPropertyBuilder<Brush>> valueBrush)
    {
        var connector = new TextBlock().Style(AppTypography.Styles.HeadingText).FontSize(18)
            .Foreground(AppColors.Brushes.SecondaryText)
            .VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => step.Connector))
            .Visibility(x => x.Binding(() => step.Connector).Convert(ShowIfHasText));
        var content = new StackPanel().Spacing(3).Children(
            Caption().Text(x => x.Binding(() => step.Label)),
            new TextBlock().Style(AppTypography.Styles.HeadingText).FontSize(16).Foreground(valueBrush).TextWrapping(TextWrapping.Wrap)
                .Text(x => x.Binding(() => step.Text)),
            Caption().Text(x => x.Binding(() => step.Note)).Visibility(x => x.Binding(() => step.Note).Convert(ShowIfHasText)));
        return Panel().Padding(12).Child(new Grid().ColumnDefinitions("Auto, *").ColumnSpacing(8)
            .Children(connector, content.Grid(column: 1)));
    }

    private static Border SectionCard(FinancialReportSection section) =>
        Panel(panelRadius: true).Padding(16).Child(SectionContent(section));

    private static Border DaySectionCard(FinancialReportSection section) =>
        Panel().Padding(14).Child(SectionContent(section));

    private static StackPanel SectionContent(FinancialReportSection section)
    {
        var title = new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 0, 0, 10))
            .Margin(new Thickness(0, 0, 0, 2))
            .Child(Label().FontSize(14).FontWeight(FontWeights.SemiBold).Text(x => x.Binding(() => section.Title)));

        var rows = new ItemsControl()
            .ItemsSource(x => x.Binding(() => section.Values))
            .ItemTemplate<FinancialReportValue>(ValueRow);

        return new StackPanel().Spacing(4).Children(title, rows);
    }

    private static Border ValueRow(FinancialReportValue value) =>
        new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 7, 0, 7))
            .Child(new Grid().ColumnDefinitions("*, Auto").ColumnSpacing(12).Children(
                Caption().Text(x => x.Binding(() => value.Label)),
                Label().FontWeight(FontWeights.SemiBold).TextAlignment(TextAlignment.Right)
                    .Text(x => x.Binding(() => value.Text)).Grid(column: 1)));

    private static Border BuildDayColumnHeader()
    {
        string[] labels = ["Ngày", "Hàng bán", "Hàng hoàn", "Doanh thu thuần", "Lãi gộp sau hoàn"];
        var grid = new Grid().ColumnSpacing(16);
        ConfigureDayColumns(grid);
        for (var i = 0; i < labels.Length; i++)
        {
            var cell = Caption().FontWeight(FontWeights.SemiBold).Text(labels[i])
                .TextAlignment(i == 0 ? TextAlignment.Left : TextAlignment.Right);
            SetCell(cell, 0, i);
            grid.Children.Add(cell);
        }

        // Right margin leaves room for the chevron of the expander rows below so that the columns line up.
        return new Border().Margin(new Thickness(16, 0, 52, 0)).Child(grid);
    }

    private static void ConfigureDayColumns(Grid grid)
    {
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    private static Expander BuildDay(FinancialReportDayRow day)
    {
        var dateText = Label().FontWeight(FontWeights.SemiBold).VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => day.Date));
        var salesText = Label().TextAlignment(TextAlignment.Right).VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => day.Sales));
        var refundsText = Label().TextAlignment(TextAlignment.Right).VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => day.Refunds));
        var netSalesText = Label().TextAlignment(TextAlignment.Right).VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => day.NetSales));
        var profitText = Label().FontWeight(FontWeights.SemiBold).TextAlignment(TextAlignment.Right).VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => day.NetProfit));

        // Narrow layout: the same figures with their names in front, because the column header is hidden there.
        var profitNarrow = Label().FontSize(13).FontWeight(FontWeights.SemiBold).TextAlignment(TextAlignment.Right)
            .Text(x => x.Binding(() => day.NetProfit).Convert((string amount) => $"Lãi gộp sau hoàn {amount}"));
        var narrowFigures = new StackPanel().Orientation(Orientation.Horizontal).Spacing(14).Children(
            Caption().Text(x => x.Binding(() => day.Sales).Convert((string amount) => $"Bán {amount}")),
            Caption().Text(x => x.Binding(() => day.Refunds).Convert((string amount) => $"Hoàn {amount}")),
            Caption().Text(x => x.Binding(() => day.NetSales).Convert((string amount) => $"Thuần {amount}")));

        var header = new Grid().ColumnSpacing(16).RowSpacing(4).Children(
            dateText, salesText, refundsText, netSalesText, profitText, profitNarrow, narrowFigures);

        void LayoutHeader(double width)
        {
            var wide = width >= DayRowMinWidth;
            header.ColumnDefinitions.Clear();
            header.RowDefinitions.Clear();
            salesText.Visibility = refundsText.Visibility = netSalesText.Visibility = profitText.Visibility =
                wide ? Visibility.Visible : Visibility.Collapsed;
            profitNarrow.Visibility = narrowFigures.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
            if (wide)
            {
                ConfigureDayColumns(header);
                header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SetCell(dateText, 0, 0);
                SetCell(salesText, 0, 1);
                SetCell(refundsText, 0, 2);
                SetCell(netSalesText, 0, 3);
                SetCell(profitText, 0, 4);
            }
            else
            {
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SetCell(dateText, 0, 0);
                SetCell(profitNarrow, 0, 1);
                SetCell(narrowFigures, 1, 0);
                Grid.SetColumnSpan(narrowFigures, 2);
            }
        }

        LayoutHeader(800);
        header.SizeChanged += (_, args) => LayoutHeader(args.NewSize.Width);

        var detailGrid = BuildFourCardGrid(
            CardHost().Content(x => x.Binding(() => day.SalesSection)).ContentTemplate<FinancialReportSection>(DaySectionCard),
            CardHost().Content(x => x.Binding(() => day.CashFlowSection)).ContentTemplate<FinancialReportSection>(DaySectionCard),
            CardHost().Content(x => x.Binding(() => day.CostProfitSection)).ContentTemplate<FinancialReportSection>(DaySectionCard),
            CardHost().Content(x => x.Binding(() => day.WarrantySection)).ContentTemplate<FinancialReportSection>(DaySectionCard));
        detailGrid.SizeChanged += (_, args) => UpdateTwoColumnGrid(detailGrid, args.NewSize.Width);

        var expander = new Expander()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .Margin(new Thickness(0, 0, 0, 8))
            .Header(header)
            .Content(detailGrid);
        // Uno Markup 6.7 generates no fluent extension for the AutomationProperties attached properties.
        expander.SetBinding(AutomationProperties.NameProperty, new Binding { Path = new PropertyPath(nameof(day.Date)) });
        return expander;
    }

    private static Border RankedCard(FinancialReportRankedList list)
    {
        var header = new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 0, 0, 10))
            .Child(Label().FontSize(14).FontWeight(FontWeights.SemiBold).Text(x => x.Binding(() => list.Title)));

        var empty = Caption().Padding(new Thickness(0, 12, 0, 4))
            .Text(x => x.Binding(() => list.EmptyMessage))
            .Visibility(x => x.Binding(() => list.IsEmpty).Convert(ShowIf));

        // The thin bar is a display-only share of the largest figure of the list (Ratio 0..1), never a new figure.
        var rows = new ItemsControl()
            .ItemsSource(x => x.Binding(() => list.Rows))
            .ItemTemplate<FinancialReportRankedRow>(row =>
                new Border()
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(0, 0, 0, 1))
                    .Padding(new Thickness(0, 9, 0, 9))
                    .Child(new StackPanel().Spacing(4).Children(
                        Label().FontWeight(FontWeights.SemiBold).Text(x => x.Binding(() => row.Title)),
                        Caption().Text(x => x.Binding(() => row.Figures)),
                        new ProgressBar { Minimum = 0, Maximum = 1, Height = 4, MinHeight = 4, IsTabStop = false }
                            .Foreground(AppColors.Brushes.Primary)
                            .Background(AppColors.Brushes.Subtle)
                            .Value(x => x.Binding(() => row.Ratio)))));

        return Panel(panelRadius: true).Padding(16)
            .Child(new StackPanel().Spacing(4).Children(header, empty, rows));
    }

    private static TextBox DateInput(string label, string automationId)
    {
        var input = new TextBox { Header = label, PlaceholderText = "dd/MM/yyyy", MaxLength = 10 };
        AutomationProperties.SetName(input, label);
        AutomationProperties.SetAutomationId(input, automationId);
        AutomationProperties.SetHelpText(input, "Định dạng dd/MM/yyyy");
        return input;
    }

    private static TextBlock DateError()
    {
        var error = Caption().Foreground(AppColors.Brushes.DangerText);
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
        return error;
    }

    private static StackPanel SectionHeading(string title, string subtitle) => new StackPanel().Spacing(2).Children(
        new TextBlock().Style(AppTypography.Styles.SectionText).FontSize(18).Foreground(AppColors.Brushes.Text)
            .TextWrapping(TextWrapping.Wrap).Text(title),
        Caption().Text(subtitle));

    private static Border Panel(bool panelRadius = false) => new Border()
        .Background(AppColors.Brushes.Surface)
        .BorderBrush(AppColors.Brushes.Stroke)
        .BorderThickness(new Thickness(1))
        .CornerRadius(panelRadius ? AppMetrics.Values.PanelRadius : AppMetrics.Values.CardRadius);

    private static TextBlock Label() => new TextBlock().Foreground(AppColors.Brushes.Text).TextWrapping(TextWrapping.Wrap);

    private static TextBlock Caption() => new TextBlock().Style(AppResources.GetStyle(AppTypography.CaptionText))
        .Foreground(AppColors.Brushes.SecondaryText).TextWrapping(TextWrapping.Wrap);

    private static void SetCell(UIElement element, int row, int column)
    {
        if (element is not FrameworkElement frameworkElement) return;
        Grid.SetRow(frameworkElement, row);
        Grid.SetColumn(frameworkElement, column);
    }

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    private static Visibility ShowIfHasText(string? value) => ShowIf(!string.IsNullOrWhiteSpace(value));
}

/// <summary>
/// Tab of the Overview / Financial report selector. Selection is shown by a raised surface, a leading dot
/// and a semibold label, all bound to <see cref="IsSelected"/> with theme brushes set directly on elements.
/// </summary>
internal sealed class SegmentedTabButton : Button
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(SegmentedTabButton),
        // Only raises the UI Automation selection event; every visual is a binding to IsSelected.
        new PropertyMetadata(false, (d, e) => ((SegmentedTabButton)d).RaiseSelectionChanged((bool)e.OldValue, (bool)e.NewValue)));

    public SegmentedTabButton(string label, string automationId)
    {
        var selectedSurface = new Border()
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(7))
            .Visibility(x => x.Binding(() => Self.IsSelected).Convert(ShowIf));

        var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center }
            .Background(AppColors.Brushes.Primary)
            .Visibility(x => x.Binding(() => Self.IsSelected).Convert(ShowIf));

        // Two stacked labels so both states use theme brushes; the hidden semibold one keeps the width stable.
        var selectedLabel = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }
            .Foreground(AppColors.Brushes.Text)
            .Visibility(x => x.Binding(() => Self.IsSelected).Convert(ShowIf));
        var normalLabel = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
            .Foreground(AppColors.Brushes.SecondaryText)
            .Visibility(x => x.Binding(() => Self.IsSelected).Convert(HideIf));
        var sizer = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Opacity = 0, IsHitTestVisible = false };
        var labels = new Grid().Children(sizer, selectedLabel, normalLabel);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Margin(new Thickness(14, 7, 14, 7))
            .Children(dot, labels);
        Content = new Grid().DataContext(this).Children(selectedSurface, row);

        this.Background(AppColors.Brushes.Subtle);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(7);
        Padding = new Thickness(0);
        MinHeight = 34;

        AutomationProperties.SetName(this, label);
        AutomationProperties.SetAutomationId(this, automationId);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // Binding source for the inner tree (its DataContext is this control; the control's own DataContext stays the page's).
    private SegmentedTabButton Self => this;

    protected override AutomationPeer OnCreateAutomationPeer() => new SegmentedTabAutomationPeer(this);

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    private static Visibility HideIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private void RaiseSelectionChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue && FrameworkElementAutomationPeer.FromElement(this) is SegmentedTabAutomationPeer peer)
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldValue, newValue);
    }
}

internal sealed class SegmentedTabAutomationPeer(SegmentedTabButton owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
{
    public bool IsSelected => owner.IsSelected;

    public IRawElementProviderSimple SelectionContainer =>
        VisualTreeHelper.GetParent(owner) is UIElement parent
        && FrameworkElementAutomationPeer.CreatePeerForElement(parent) is { } parentPeer
            ? ProviderFromPeer(parentPeer)
            : null!;

    public void AddToSelection() => Select();

    public void RemoveFromSelection() { }

    public void Select()
    {
        if (owner.Command?.CanExecute(owner.CommandParameter) == true)
            owner.Command.Execute(owner.CommandParameter);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

    protected override object GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.SelectionItem ? this : base.GetPatternCore(patternInterface);
}
