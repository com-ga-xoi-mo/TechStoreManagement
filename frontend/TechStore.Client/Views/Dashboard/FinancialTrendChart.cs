using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TechStore.Client.Controls.Charts;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Dashboard;

/// <summary>
/// Daily trend of the financial report: grouped bars with a zero baseline and, in the sales mode, a gross profit line.
/// All coordinates come from <see cref="FinancialChartLayout"/>; this control only draws them and redraws when its
/// dependency properties, its size or the theme change. Every day also gets a transparent focusable button whose tooltip
/// and automation name hold the figures, so hover and keyboard show the same details.
/// </summary>
internal sealed class FinancialTrendChart : UserControl
{
    public static readonly DependencyProperty DaysProperty = DependencyProperty.Register(
        nameof(Days), typeof(IReadOnlyList<FinancialChartDay>), typeof(FinancialTrendChart),
        new PropertyMetadata(null, (d, _) => ((FinancialTrendChart)d).Redraw()));

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(FinancialChartMode), typeof(FinancialTrendChart),
        new PropertyMetadata(FinancialChartMode.Sales, (d, _) => ((FinancialTrendChart)d).Redraw()));

    private const double PlotLeft = 58;
    private const double PlotRight = 12;
    private const double PlotTop = 14;
    private const double PlotBottom = 30;
    private const double LabelWidth = 44;

    private readonly Canvas _canvas = new() { Height = 260 };
    private readonly StackPanel _legend = new() { Orientation = Orientation.Horizontal, Spacing = 16 };

    public FinancialTrendChart()
    {
        this.IsTabStop(false).HorizontalContentAlignment(HorizontalAlignment.Stretch);
        Content = new StackPanel { Spacing = 8 }.Children(_canvas, _legend);
        _canvas.SizeChanged += (_, _) => Redraw();
        ActualThemeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    public IReadOnlyList<FinancialChartDay>? Days
    {
        get => (IReadOnlyList<FinancialChartDay>?)GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public FinancialChartMode Mode
    {
        get => (FinancialChartMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        _legend.Children.Clear();

        var days = Days ?? [];
        var mode = Mode;
        var layout = FinancialChartLayout.Calculate(days, mode, _canvas.ActualWidth, _canvas.Height,
            PlotLeft, PlotRight, PlotTop, PlotBottom, LabelWidth);
        AddLegend(mode);
        if (layout.IsEmpty) return;

        var plotRight = layout.PlotLeft + layout.PlotWidth;
        foreach (var tick in layout.Ticks)
        {
            var grid = new Line { X1 = layout.PlotLeft, X2 = plotRight, Y1 = tick.Y, Y2 = tick.Y, StrokeThickness = 1, Opacity = 0.6 };
            grid.Stroke(AppColors.Brushes.Stroke);
            _canvas.Children.Add(grid);

            var label = new TextBlock { Text = FinancialChartFormat.Axis(tick.Value), FontSize = 10, Width = PlotLeft - 8, TextAlignment = TextAlignment.Right }
                .Foreground(AppColors.Brushes.SecondaryText);
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, tick.Y - 8);
            _canvas.Children.Add(label);
        }

        foreach (var bar in layout.Bars)
        {
            var rectangle = new Rectangle { Width = bar.Width, Height = bar.Height };
            rectangle.Fill(SeriesBrush(bar.Series));
            Canvas.SetLeft(rectangle, bar.X);
            Canvas.SetTop(rectangle, bar.Y);
            _canvas.Children.Add(rectangle);
        }

        var baseline = new Line { X1 = layout.PlotLeft, X2 = plotRight, Y1 = layout.BaselineY, Y2 = layout.BaselineY, StrokeThickness = 1.5 };
        baseline.Stroke(AppColors.Brushes.SecondaryText);
        Canvas.SetZIndex(baseline, 5);
        _canvas.Children.Add(baseline);

        foreach (var segment in layout.LineSegments)
        {
            var line = new Line { X1 = segment.X1, Y1 = segment.Y1, X2 = segment.X2, Y2 = segment.Y2, StrokeThickness = 2.5 };
            line.Stroke(SeriesBrush(FinancialChartSeries.NetProfit));
            Canvas.SetZIndex(line, 10);
            _canvas.Children.Add(line);
        }

        foreach (var point in layout.LinePoints)
        {
            var marker = new Ellipse { Width = 8, Height = 8, StrokeThickness = 2 };
            marker.Fill(AppColors.Brushes.Surface);
            marker.Stroke(SeriesBrush(FinancialChartSeries.NetProfit));
            Canvas.SetLeft(marker, point.X - 4);
            Canvas.SetTop(marker, point.Y - 4);
            Canvas.SetZIndex(marker, 11);
            _canvas.Children.Add(marker);
        }

        var labelTop = layout.PlotTop + layout.PlotHeight + 8;
        for (var index = 0; index < days.Count; index++)
        {
            if (!layout.ShowsLabel(index)) continue;
            var text = new TextBlock { Text = days[index].Label, FontSize = 10, Width = LabelWidth, TextAlignment = TextAlignment.Center }
                .Foreground(AppColors.Brushes.SecondaryText);
            Canvas.SetLeft(text, layout.DayCenters[index] - LabelWidth / 2);
            Canvas.SetTop(text, labelTop);
            _canvas.Children.Add(text);
        }

        for (var index = 0; index < days.Count; index++)
        {
            var description = mode == FinancialChartMode.Sales ? days[index].SalesDescription : days[index].CashFlowDescription;
            if (description.Length == 0) description = days[index].Label;
            var hit = new Button { Width = layout.SlotWidth, Height = layout.PlotHeight, MinWidth = 0, MinHeight = 0 }
                .Style(AppControlStyles.Styles.QuietButton)
                .Padding(new Thickness(0))
                .BorderThickness(new Thickness(0))
                .CornerRadius(new CornerRadius(0));
            hit.MinWidth = 0;
            hit.MinHeight = 0;
            ToolTipService.SetToolTip(hit, description);
            AutomationProperties.SetName(hit, description);
            AutomationProperties.SetAutomationId(hit, $"FinancialChartDay{index}");
            Canvas.SetLeft(hit, layout.DayCenters[index] - layout.SlotWidth / 2);
            Canvas.SetTop(hit, layout.PlotTop);
            Canvas.SetZIndex(hit, 20);
            _canvas.Children.Add(hit);
        }
    }

    private void AddLegend(FinancialChartMode mode)
    {
        if (mode == FinancialChartMode.Sales)
        {
            _legend.Children.Add(LegendItem(FinancialChartSeries.Sales, "Hàng bán chưa thuế", isLine: false));
            _legend.Children.Add(LegendItem(FinancialChartSeries.Refunds, "Hàng hoàn chưa thuế", isLine: false));
            _legend.Children.Add(LegendItem(FinancialChartSeries.NetProfit, "Lãi gộp sau hoàn", isLine: true));
        }
        else
        {
            _legend.Children.Add(LegendItem(FinancialChartSeries.Cash, "Thu tiền mặt", isLine: false));
            _legend.Children.Add(LegendItem(FinancialChartSeries.Qr, "Thu VietQR", isLine: false));
            _legend.Children.Add(LegendItem(FinancialChartSeries.RefundPaid, "Tiền đã chi hoàn", isLine: false));
        }
    }

    private static StackPanel LegendItem(FinancialChartSeries series, string text, bool isLine)
    {
        var mark = new Rectangle { Width = isLine ? 16 : 10, Height = isLine ? 3 : 10, VerticalAlignment = VerticalAlignment.Center };
        mark.Fill(SeriesBrush(series));
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }.Children(mark, label);
    }

    internal static Action<IDependencyPropertyBuilder<Brush>> SeriesBrush(FinancialChartSeries series) => series switch
    {
        FinancialChartSeries.Sales => AppColors.Brushes.Primary,
        FinancialChartSeries.Refunds => AppColors.Brushes.WarningText,
        FinancialChartSeries.NetProfit => AppColors.Brushes.SuccessText,
        FinancialChartSeries.Cash => AppColors.Brushes.SuccessText,
        FinancialChartSeries.Qr => AppColors.Brushes.AccentText,
        _ => AppColors.Brushes.DangerText
    };
}
