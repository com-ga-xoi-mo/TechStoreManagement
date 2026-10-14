using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TechStore.Client.Controls.Charts;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Windows.Foundation;

namespace TechStore.Client.Views.Dashboard;

public sealed partial class DashboardPage : Page, IRoutePage
{
    private const double ChartLeft = 42;
    private const double ChartRight = 12;
    private const double ChartTop = 12;
    private const double ChartBottom = 34;

    public DashboardViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ViewModel.PropertyChanged += OnViewModelChanged;
        RenderRevenueChart();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ClearChartElements();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ViewModel.Snapshot) or nameof(ViewModel.IsLoading))
            RenderRevenueChart();
    }

    private void OnPeriodSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is ComboBox selector && selector.SelectedIndex != ViewModel.SelectedPeriodIndex)
            _ = ViewModel.SelectPeriodCommand.ExecuteAsync(selector.SelectedIndex);
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var width = args.NewSize.Width;
        var metricColumns = width >= 1050 ? 4 : width >= 600 ? 2 : 1;
        ArrangeCards(metricColumns);

        var sideBySide = width >= 1120;
        MainPanelGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        MainPanelGrid.ColumnDefinitions[1].Width = sideBySide ? new GridLength(360) : new GridLength(0);
        Grid.SetRow(LowStockPanel, sideBySide ? 0 : 1);
        Grid.SetColumn(LowStockPanel, sideBySide ? 1 : 0);
        RenderRevenueChart();
    }

    private void ArrangeCards(int columns)
    {
        var rows = (4 + columns - 1) / columns;
        while (MetricGrid.RowDefinitions.Count > rows)
            MetricGrid.RowDefinitions.RemoveAt(MetricGrid.RowDefinitions.Count - 1);
        while (MetricGrid.RowDefinitions.Count < rows)
            MetricGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cards = new[] { SalesCard, OrdersCard, ProductsCard, InventoryCard };
        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetRow(cards[index], index / columns);
            Grid.SetColumn(cards[index], index % columns);
        }

        for (var index = 0; index < MetricGrid.ColumnDefinitions.Count; index++)
            MetricGrid.ColumnDefinitions[index].Width = index < columns
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
    }

    private void OnRevenueCanvasSizeChanged(object sender, SizeChangedEventArgs args) => RenderRevenueChart();

    private void RenderRevenueChart()
    {
        if (!IsLoaded || RevenueCanvas.ActualWidth <= ChartLeft + ChartRight || RevenueCanvas.ActualHeight <= ChartTop + ChartBottom)
            return;

        ClearChartElements();
        var values = ViewModel.Snapshot?.DailyRevenue;
        if (values is null || values.Count == 0)
            return;

        var layout = RevenueChartLayout.Calculate(values, RevenueCanvas.ActualWidth, RevenueCanvas.ActualHeight,
            ChartLeft, ChartRight, ChartTop, ChartBottom);
        if (layout.Points.Count == 0)
            return;

        var plotWidth = RevenueCanvas.ActualWidth - ChartLeft - ChartRight;
        var plotHeight = RevenueCanvas.ActualHeight - ChartTop - ChartBottom;
        var brush = RevenueTrendLine.Stroke;
        var points = new PointCollection();
        var slotWidth = plotWidth / values.Count;

        for (var level = 0; level <= 4; level++)
        {
            var y = ChartTop + plotHeight * (4 - level) / 4;
            RevenueCanvas.Children.Add(new Line
            {
                X1 = ChartLeft,
                X2 = ChartLeft + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = brush,
                StrokeThickness = level == 0 ? 1.5 : 0.6,
                Opacity = level == 0 ? 0.55 : 0.18
            });
            if (level is 0 or 2 or 4)
                AddLabel($"{layout.AxisInterval * level / 1_000_000m:0.#}M", 0, y - 10, ChartLeft - 6, 18, TextAlignment.Right);
        }

        for (var index = 0; index < values.Count; index++)
        {
            var point = values[index];
            var x = layout.Points[index].X;
            var y = layout.Points[index].Y;
            var barWidth = Math.Min(24, slotWidth * 0.44);
            var barHeight = Math.Max(1, ChartTop + plotHeight - y);
            var bar = new Rectangle { Width = barWidth, Height = barHeight, Fill = brush, Opacity = 0.16 };
            Canvas.SetLeft(bar, x - barWidth / 2);
            Canvas.SetTop(bar, y);
            RevenueCanvas.Children.Add(bar);
            points.Add(new Point(x, y));

            if (index % Math.Max(1, values.Count / 6) == 0 || index == values.Count - 1)
            {
                var dateLabel = point.Date.ToString("dd/MM");
                AddLabel(dateLabel, x - slotWidth / 2, ChartTop + plotHeight + 7, slotWidth, 20, TextAlignment.Center);
            }
        }

        RevenueTrendLine.Points = points;
        Canvas.SetZIndex(RevenueTrendLine, int.MaxValue);
    }

    private void ClearChartElements()
    {
        foreach (var element in RevenueCanvas.Children.Where(element => !ReferenceEquals(element, RevenueTrendLine)).ToArray())
            RevenueCanvas.Children.Remove(element);
        RevenueTrendLine.Points.Clear();
    }

    private void AddLabel(string text, double left, double top, double width, double height, TextAlignment alignment)
    {
        var label = new TextBlock
        {
            Text = text,
            Width = width,
            Height = height,
            FontSize = 11,
            TextAlignment = alignment,
            Foreground = (Brush)Application.Current.Resources["TechStoreSecondaryTextBrush"]
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        RevenueCanvas.Children.Add(label);
    }
}
