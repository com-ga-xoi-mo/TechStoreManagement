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
    public FrameworkElement BuildTabs()
    {
        var overview = new SegmentedTabButton("Tổng quan", "DashboardOverviewTab")
            .Command(x => x.Binding(() => report.CloseCommand));
        overview.SetBinding(SegmentedTabButton.IsSelectedProperty,
            new Binding { Path = new PropertyPath(nameof(report.ShowOverview)), Mode = BindingMode.OneWay });

        var financial = new SegmentedTabButton("Báo cáo tài chính", "DashboardFinancialReportTab")
            .Command(x => x.Binding(() => report.OpenCommand));
        financial.SetBinding(SegmentedTabButton.IsSelectedProperty,
            new Binding { Path = new PropertyPath(nameof(report.IsOpen)), Mode = BindingMode.OneWay });

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
        var summaryGrid = BuildFourCardGrid(
            SectionCard(nameof(FinancialReportPresentation.SalesSection)),
            SectionCard(nameof(FinancialReportPresentation.CashFlowSection)),
            SectionCard(nameof(FinancialReportPresentation.CostProfitSection)),
            SectionCard(nameof(FinancialReportPresentation.WarrantySection)));
        var dailyList = new ItemsControl().ItemsSource(x => x.Binding(() => report.Presentation!.Daily))
            .ItemTemplate<FinancialReportDayRow>(BuildDay);
        var listsGrid = BuildFourCardGrid(
            ListCard(nameof(FinancialReportPresentation.TopQuantityList)),
            ListCard(nameof(FinancialReportPresentation.TopRevenueList)),
            ListCard(nameof(FinancialReportPresentation.CashierList)),
            ListCard(nameof(FinancialReportPresentation.ShiftList)));

        var snapshot = new StackPanel().Spacing(20)
            .Visibility(x => x.Binding(() => report.HasSnapshot).Convert(ShowIf))
            .Children(
                emptyNotice,
                allocationNotice,
                kpiGrid,
                SectionHeading("Tổng kỳ chi tiết", "Các chỉ số tài chính tổng hợp do máy chủ tính cho toàn bộ kỳ."),
                summaryGrid,
                SectionHeading("Theo ngày", "Chọn từng ngày để mở đầy đủ chỉ số bán, hoàn, dòng tiền và giá vốn."),
                dailyList,
                SectionHeading("Xếp hạng & Ca tiền mặt", "Top sản phẩm theo số lượng, doanh thu, hiệu suất thu ngân và ca tiền mặt."),
                listsGrid);

        var body = new StackPanel().Padding(new Thickness(20, 16, 20, 24)).Spacing(16)
            .Children(header, filter, loading, error, snapshot);
        body.SizeChanged += (_, args) =>
        {
            UpdateKpiLayout(kpiGrid, args.NewSize.Width);
            UpdateTwoColumnGrid(summaryGrid, args.NewSize.Width);
            UpdateTwoColumnGrid(listsGrid, args.NewSize.Width);
        };

        return new ScrollViewer().DataContext(report).VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .Content(body);
    }

    private Border BuildFilter()
    {
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
        AutomationProperties.SetAutomationId(apply, "FinancialReportApply");
        AutomationProperties.SetAutomationId(reload, "FinancialReportReload");

        var buttons = new StackPanel().Orientation(Orientation.Horizontal).Spacing(8)
            .VerticalAlignment(VerticalAlignment.Top).Children(apply, reload);

        var reference = Label().FontSize(12).FontWeight(FontWeights.SemiBold)
            .Text(x => x.Binding(() => report.Presentation!.Reference))
            .Visibility(x => x.Binding(() => report.HasSnapshot).Convert(ShowIf));
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
        return Panel(panelRadius: true).Padding(16).Child(grid);
    }

    private Grid BuildKpiGrid()
    {
        var grid = new Grid().ColumnSpacing(12).RowSpacing(12).Children(
            KpiCard(nameof(FinancialReportPresentation.NetSalesKpi), AppColors.Brushes.Primary),
            KpiCard(nameof(FinancialReportPresentation.NetProfitKpi), AppColors.Brushes.SuccessText),
            KpiCard(nameof(FinancialReportPresentation.CashFlowKpi), AppColors.Brushes.Text),
            KpiCard(nameof(FinancialReportPresentation.WarrantyAndOrdersKpi), AppColors.Brushes.WarningText));
        UpdateKpiLayout(grid, 1000);
        return grid;
    }

    private static void UpdateKpiLayout(Grid grid, double width)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (width >= 960)
        {
            for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < 4; i++) SetCell(grid.Children[i], 0, i);
        }
        else if (width >= 500)
        {
            for (var i = 0; i < 2; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SetCell(grid.Children[0], 0, 0);
            SetCell(grid.Children[1], 0, 1);
            SetCell(grid.Children[2], 1, 0);
            SetCell(grid.Children[3], 1, 1);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 4; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SetCell(grid.Children[i], i, 0);
            }
        }
    }

    private static Grid BuildFourCardGrid(FrameworkElement a, FrameworkElement b, FrameworkElement c, FrameworkElement d)
    {
        var grid = new Grid().ColumnSpacing(12).RowSpacing(12).Children(a, b, c, d);
        UpdateTwoColumnGrid(grid, 1000);
        return grid;
    }

    private static void UpdateTwoColumnGrid(Grid grid, double width)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (width >= 700)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SetCell(grid.Children[0], 0, 0);
            SetCell(grid.Children[1], 0, 1);
            SetCell(grid.Children[2], 1, 0);
            SetCell(grid.Children[3], 1, 1);
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

    private static Border KpiCard(string kpiPropertyName, Action<IDependencyPropertyBuilder<Brush>> valueBrush)
    {
        var card = Panel(panelRadius: true).Padding(16).Child(new StackPanel().Spacing(6).Children(
            BindText(nameof(FinancialReportKpiCard.Title), Caption()),
            BindText(nameof(FinancialReportKpiCard.Value), new TextBlock().Style(AppTypography.Styles.HeadingText).FontSize(22).Foreground(valueBrush).TextWrapping(TextWrapping.Wrap)),
            BindText(nameof(FinancialReportKpiCard.Detail), Caption())));
        card.SetBinding(FrameworkElement.DataContextProperty,
            new Binding { Path = new PropertyPath($"Presentation.{kpiPropertyName}"), Mode = BindingMode.OneWay });
        return card;
    }

    private static Border SectionCard(string sectionPropertyName)
    {
        var card = Panel(panelRadius: true).Padding(16).Child(SectionContent());
        card.SetBinding(FrameworkElement.DataContextProperty,
            new Binding { Path = new PropertyPath($"Presentation.{sectionPropertyName}"), Mode = BindingMode.OneWay });
        return card;
    }

    private static StackPanel SectionContent()
    {
        var title = new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 0, 0, 10))
            .Margin(new Thickness(0, 0, 0, 2))
            .Child(BindText(nameof(FinancialReportSection.Title),
                Label().FontSize(14).FontWeight(FontWeights.SemiBold)));

        var rows = new ItemsControl()
            .ItemTemplate<FinancialReportValue>(value =>
                new Border()
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(0, 0, 0, 1))
                    .Padding(new Thickness(0, 7, 0, 7))
                    .Child(new Grid().ColumnDefinitions("*, Auto").ColumnSpacing(12).Children(
                        Caption().Text(x => x.Binding(() => value.Label)),
                        Label().FontWeight(FontWeights.SemiBold).TextAlignment(TextAlignment.Right)
                            .Text(x => x.Binding(() => value.Text)).Grid(column: 1))));
        rows.SetBinding(ItemsControl.ItemsSourceProperty,
            new Binding { Path = new PropertyPath(nameof(FinancialReportSection.Values)), Mode = BindingMode.OneWay });

        return new StackPanel().Spacing(4).Children(title, rows);
    }

    private static Expander BuildDay(FinancialReportDayRow day)
    {
        var dateText = Label().FontWeight(FontWeights.SemiBold).VerticalAlignment(VerticalAlignment.Center)
            .Text(x => x.Binding(() => day.Date));
        var salesText = Caption().Text(x => x.Binding(() => day.Sales).Convert((string amount) => $"Bán {amount}"));
        var refundsText = Caption().Text(x => x.Binding(() => day.Refunds).Convert((string amount) => $"Hoàn {amount}"));
        var netText = Label().FontSize(13).FontWeight(FontWeights.SemiBold)
            .Text(x => x.Binding(() => day.NetProfit).Convert((string amount) => $"Lãi sau hoàn {amount}"));

        var header = new Grid().ColumnSpacing(16).RowSpacing(4).Children(dateText, salesText, refundsText, netText);

        void LayoutHeader(double width)
        {
            header.ColumnDefinitions.Clear();
            header.RowDefinitions.Clear();
            if (width >= 620)
            {
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                SetCell(dateText, 0, 0);
                SetCell(salesText, 0, 1);
                SetCell(refundsText, 0, 2);
                SetCell(netText, 0, 3);
                salesText.VerticalAlignment = VerticalAlignment.Center;
                refundsText.VerticalAlignment = VerticalAlignment.Center;
                netText.VerticalAlignment = VerticalAlignment.Center;
                netText.TextAlignment = TextAlignment.Right;
            }
            else
            {
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                SetCell(dateText, 0, 0);
                SetCell(netText, 0, 1);
                SetCell(salesText, 1, 0);
                SetCell(refundsText, 1, 1);
                netText.TextAlignment = TextAlignment.Right;
            }
        }

        LayoutHeader(800);
        header.SizeChanged += (_, args) => LayoutHeader(args.NewSize.Width);

        var detailGrid = BuildFourCardGrid(
            DailySectionCard(nameof(FinancialReportDayRow.SalesSection)),
            DailySectionCard(nameof(FinancialReportDayRow.CashFlowSection)),
            DailySectionCard(nameof(FinancialReportDayRow.CostProfitSection)),
            DailySectionCard(nameof(FinancialReportDayRow.WarrantySection)));
        detailGrid.SizeChanged += (_, args) => UpdateTwoColumnGrid(detailGrid, args.NewSize.Width);

        var expander = new Expander()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .Margin(new Thickness(0, 0, 0, 8))
            .Header(header)
            .Content(detailGrid);
        expander.SetBinding(AutomationProperties.NameProperty, new Binding { Path = new PropertyPath(nameof(day.Date)) });
        return expander;
    }

    private static Border DailySectionCard(string sectionPropertyName)
    {
        var card = Panel().Padding(14).Child(SectionContent());
        card.SetBinding(FrameworkElement.DataContextProperty,
            new Binding { Path = new PropertyPath(sectionPropertyName), Mode = BindingMode.OneWay });
        return card;
    }

    private static Border ListCard(string listPropertyName)
    {
        var header = new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 0, 0, 10))
            .Child(BindText(nameof(FinancialReportList.Title),
                Label().FontSize(14).FontWeight(FontWeights.SemiBold)));

        var empty = Caption().Padding(new Thickness(0, 12, 0, 4));
        empty.SetBinding(TextBlock.TextProperty,
            new Binding { Path = new PropertyPath(nameof(FinancialReportList.EmptyMessage)), Mode = BindingMode.OneWay });
        empty.SetBinding(UIElement.VisibilityProperty,
            new Binding { Path = new PropertyPath(nameof(FinancialReportList.IsEmpty)), Mode = BindingMode.OneWay, Converter = BoolToVisibilityConverter.Instance });

        var rows = new ItemsControl()
            .ItemTemplate<FinancialReportListRow>(row =>
                new Border()
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(0, 0, 0, 1))
                    .Padding(new Thickness(0, 9, 0, 9))
                    .Child(new StackPanel().Spacing(3).Children(
                        Label().FontWeight(FontWeights.SemiBold).Text(x => x.Binding(() => row.Title)),
                        Caption().Text(x => x.Binding(() => row.Figures)))));
        rows.SetBinding(ItemsControl.ItemsSourceProperty,
            new Binding { Path = new PropertyPath(nameof(FinancialReportList.Rows)), Mode = BindingMode.OneWay });

        var card = Panel(panelRadius: true).Padding(16)
            .Child(new StackPanel().Spacing(4).Children(header, empty, rows));
        card.SetBinding(FrameworkElement.DataContextProperty,
            new Binding { Path = new PropertyPath($"Presentation.{listPropertyName}"), Mode = BindingMode.OneWay });
        return card;
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

    private static TextBlock BindText(string path, TextBlock target)
    {
        target.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(path), Mode = BindingMode.OneWay });
        return target;
    }

    private static void SetCell(UIElement element, int row, int column)
    {
        if (element is not FrameworkElement frameworkElement) return;
        Grid.SetRow(frameworkElement, row);
        Grid.SetColumn(frameworkElement, column);
    }

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    private static Visibility ShowIfHasText(string? value) => ShowIf(!string.IsNullOrWhiteSpace(value));

    private sealed class SegmentedTabButton : Button
    {
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
            nameof(IsSelected), typeof(bool), typeof(SegmentedTabButton),
            new PropertyMetadata(false, (d, e) => ((SegmentedTabButton)d).OnSelectionChanged((bool)e.OldValue, (bool)e.NewValue)));

        private readonly Border _indicator;
        private readonly TextBlock _label;

        public SegmentedTabButton(string label, string automationId)
        {
            _indicator = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center
            }.Background(AppColors.Brushes.Primary);

            _label = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            };

            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _indicator, _label }
            };

            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(7);
            Padding = new Thickness(14, 7, 14, 7);
            MinHeight = 34;

            AutomationProperties.SetName(this, label);
            AutomationProperties.SetAutomationId(this, automationId);
            ApplySelectionVisuals(false);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new SegmentedTabAutomationPeer(this);

        private void OnSelectionChanged(bool oldValue, bool newValue)
        {
            ApplySelectionVisuals(newValue);
            if (oldValue != newValue && FrameworkElementAutomationPeer.FromElement(this) is SegmentedTabAutomationPeer peer)
                peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldValue, newValue);
        }

        private void ApplySelectionVisuals(bool selected)
        {
            this.Background(selected ? AppColors.Brushes.Surface : AppColors.Brushes.Subtle);
            this.BorderBrush(selected ? AppColors.Brushes.Stroke : AppColors.Brushes.Subtle);
            _label.Foreground(selected ? AppColors.Brushes.Text : AppColors.Brushes.SecondaryText);
            _label.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            _indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private sealed class SegmentedTabAutomationPeer(SegmentedTabButton owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
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

    private sealed class BoolToVisibilityConverter : IValueConverter
    {
        public static BoolToVisibilityConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, string language) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
