using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TechStore.Client.Controls.Charts;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Dashboard;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Dashboard;

/// <summary>
/// Cash / VietQR ring of the money received. Angles and arc end points come from <see cref="FinancialDonutLayout"/>;
/// when a channel is not computed (or nothing was received) a neutral ring is drawn instead of invented shares.
/// The centre shows the "Tiền đã thu" figure through bindings.
/// </summary>
internal sealed class FinancialDonutChart : UserControl
{
    public static readonly DependencyProperty ChannelsProperty = DependencyProperty.Register(
        nameof(Channels), typeof(FinancialReportChannels), typeof(FinancialDonutChart),
        new PropertyMetadata(null, (d, _) => ((FinancialDonutChart)d).Redraw()));

    private const double Size = 168;
    private const double Thickness = 22;

    private readonly Canvas _canvas = new() { Width = Size, Height = Size };

    public FinancialDonutChart()
    {
        this.IsTabStop(false);
        var center = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = Size - 2 * Thickness - 16 }
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Text(x => x.Binding(() => Self.Channels!.CenterCaption)),
                new TextBlock { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, MaxWidth = Size - 2 * Thickness - 16 }
                    .Foreground(AppColors.Brushes.Text)
                    .Text(x => x.Binding(() => Self.Channels!.CenterValue)));
        var root = new Grid { Width = Size, Height = Size, HorizontalAlignment = HorizontalAlignment.Center }
            .DataContext(this)
            .Children(_canvas, center);
        Content = root;
        AutomationProperties.SetAutomationId(this, "FinancialReportChannelDonut");
        ActualThemeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    // Binding source for the inner tree (its DataContext is this control; the control's own DataContext stays the consumer's).
    private FinancialDonutChart Self => this;

    public FinancialReportChannels? Channels
    {
        get => (FinancialReportChannels?)GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        var channels = Channels;
        AutomationProperties.SetName(this, channels?.Description ?? "Kênh thu tiền");

        var radius = (Size - Thickness) / 2;
        var layout = channels?.Donut(Size / 2, Size / 2, radius) ?? FinancialDonutLayout.Calculate(null, null, null, Size / 2, Size / 2, radius);
        if (layout.IsNeutral)
        {
            var ring = new Ellipse { Width = Size - Thickness, Height = Size - Thickness, StrokeThickness = Thickness };
            ring.Stroke(AppColors.Brushes.NeutralStroke);
            Canvas.SetLeft(ring, Thickness / 2);
            Canvas.SetTop(ring, Thickness / 2);
            _canvas.Children.Add(ring);
            return;
        }

        foreach (var segment in layout.Segments)
        {
            var figure = new PathFigure { StartPoint = new Windows.Foundation.Point(segment.StartX, segment.StartY) };
            figure.Segments.Add(new ArcSegment
            {
                Point = new Windows.Foundation.Point(segment.EndX, segment.EndY),
                Size = new Windows.Foundation.Size(radius, radius),
                IsLargeArc = segment.IsLargeArc,
                SweepDirection = SweepDirection.Clockwise
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            var path = new Microsoft.UI.Xaml.Shapes.Path { Data = geometry, StrokeThickness = Thickness };
            path.Stroke(segment.Key == FinancialDonutLayout.CashKey ? AppColors.Brushes.SuccessText : AppColors.Brushes.AccentText);
            _canvas.Children.Add(path);
        }
    }
}
