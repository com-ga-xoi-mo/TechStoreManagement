using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TechStore.Client.Models.Analytics;

namespace TechStore.Client.Controls.Dashboard;

public sealed partial class RevenueChartControl : UserControl
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<RevenuePoint>), typeof(RevenueChartControl),
        new PropertyMetadata(Array.Empty<RevenuePoint>(), OnPointsChanged));

    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public IReadOnlyList<RevenuePoint> Points
    {
        get => (IReadOnlyList<RevenuePoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public RevenueChartControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderChart();
    }

    private static void OnPointsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RevenueChartControl chart)
        {
            chart.RenderChart();
        }
    }

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs args) => RenderChart();

    private void RenderChart()
    {
        if (PlotCanvas is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        PlotCanvas.Children.Clear();
        var points = Points?.OrderBy(point => point.BucketStart).ToArray() ?? Array.Empty<RevenuePoint>();
        var width = PlotCanvas.ActualWidth;
        var height = PlotCanvas.ActualHeight;
        const double left = 52;
        const double right = 12;
        const double top = 12;
        const double bottom = 34;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var maxValue = points.Length == 0 ? 1m : Math.Max(1m, points.Max(point => point.Revenue));
        var gridBrush = ResourceBrush("TechStoreBorderBrush");
        var textBrush = ResourceBrush("TechStoreTextMutedBrush");
        var primaryBrush = ResourceBrush("TechStorePrimaryBrush");
        var fillBrush = ResourceBrush("TechStorePrimaryTintBrush");

        for (var line = 0; line <= 4; line++)
        {
            var y = top + plotHeight * line / 4;
            var grid = new Line
            {
                X1 = left,
                X2 = left + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 }
            };
            PlotCanvas.Children.Add(grid);

            var amount = maxValue * (4 - line) / 4;
            var axisLabel = new TextBlock
            {
                Text = FormatAxisAmount(amount),
                FontSize = 10,
                Foreground = textBrush,
                Width = left - 8,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(axisLabel, 0);
            Canvas.SetTop(axisLabel, y - 8);
            PlotCanvas.Children.Add(axisLabel);
        }

        if (points.Length == 0)
        {
            AddCenterMessage("Chưa có dữ liệu biểu đồ", width, height, textBrush);
            return;
        }

        var slotWidth = plotWidth / points.Length;
        var barWidth = Math.Clamp(slotWidth * 0.46, 2, 28);
        var linePoints = new List<Windows.Foundation.Point>(points.Length);
        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            var x = left + slotWidth * (index + 0.5);
            var barHeight = (double)(point.Revenue / maxValue) * plotHeight;
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(0, barHeight),
                Fill = fillBrush,
                Stroke = primaryBrush,
                StrokeThickness = 1,
                RadiusX = 2,
                RadiusY = 2
            };
            AutomationProperties.SetName(bar, FormatPoint(point));
            Canvas.SetLeft(bar, x - barWidth / 2);
            Canvas.SetTop(bar, top + plotHeight - Math.Max(0, barHeight));
            PlotCanvas.Children.Add(bar);

            var y = top + plotHeight - (double)(point.Revenue / maxValue) * plotHeight;
            linePoints.Add(new Windows.Foundation.Point(x, y));

            if (ShouldShowDateLabel(index, points.Length))
            {
                var label = new TextBlock
                {
                    Text = point.BucketStart.ToString(points.Length <= 24 ? "HH:mm" : "dd/MM", VietnameseCulture),
                    FontSize = 10,
                    Foreground = textBrush,
                    Width = Math.Max(slotWidth, 32),
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(label, x - label.Width / 2);
                Canvas.SetTop(label, top + plotHeight + 8);
                PlotCanvas.Children.Add(label);
            }
        }

        if (linePoints.Count > 1)
        {
            var trendPoints = new PointCollection();
            foreach (var point in linePoints)
            {
                trendPoints.Add(point);
            }

            var trend = new Polyline
            {
                Points = trendPoints,
                Stroke = primaryBrush,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            PlotCanvas.Children.Add(trend);
        }
    }

    private void AddCenterMessage(string message, double width, double height, Brush brush)
    {
        var label = new TextBlock { Text = message, Foreground = brush, FontSize = 13 };
        label.Measure(new Windows.Foundation.Size(width, height));
        Canvas.SetLeft(label, Math.Max(0, (width - label.DesiredSize.Width) / 2));
        Canvas.SetTop(label, height / 2);
        PlotCanvas.Children.Add(label);
    }

    private static bool ShouldShowDateLabel(int index, int count)
    {
        if (count <= 8) return true;
        var interval = (int)Math.Ceiling(count / 7d);
        return index == 0 || index == count - 1 || index % interval == 0;
    }

    private static string FormatPoint(RevenuePoint point) =>
        $"{point.BucketStart.ToString("dd/MM HH:mm", VietnameseCulture)}: {point.Revenue.ToString("N0", VietnameseCulture)} đồng";

    private static string FormatAxisAmount(decimal amount) => amount >= 1_000_000m
        ? $"{(amount / 1_000_000m).ToString("0.#", VietnameseCulture)}M"
        : amount.ToString("N0", VietnameseCulture);

    private static Brush ResourceBrush(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
