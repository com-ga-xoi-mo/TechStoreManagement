using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.Text;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace TechStore.Client.Features.Categories.Pages;

internal static class CategoryWorkspaceStyles
{
    internal const string Primary = "#0066FF";
    internal const string Slate50 = "#F8FAFC";
    internal const string Slate200 = "#E2E8F0";
    internal const string Slate500 = "#64748B";
    internal const string Slate900 = "#0F172A";
    internal static readonly FontFamily Typeface = new("ms-appx:///Assets/Fonts/PlusJakartaSans.ttf");

    internal static SolidColorBrush Brush(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        if (!hex.StartsWith('#')) return new SolidColorBrush(ColorHelper.FromArgb(255, 100, 116, 139));
        try
        {
            return new(ColorHelper.FromArgb(
                hex.Length == 9 ? Convert.ToByte(hex.Substring(1, 2), 16) : (byte)255,
                Convert.ToByte(hex.Substring(hex.Length - 6, 2), 16),
                Convert.ToByte(hex.Substring(hex.Length - 4, 2), 16),
                Convert.ToByte(hex.Substring(hex.Length - 2, 2), 16)));
        }
        catch
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, 100, 116, 139));
        }
    }

    internal static TextBlock Text(string value = "", double size = 14, string color = Slate900, ushort weight = 400) => new()
    {
        Text = value, FontSize = size, Foreground = Brush(color), FontFamily = Typeface,
        FontWeight = new FontWeight { Weight = weight }, VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    internal static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    internal static Border Badge(UIElement content, string background, string border = "#00000000") => new()
    {
        Child = content, Background = Brush(background), BorderBrush = Brush(border),
        BorderThickness = new Thickness(border == "#00000000" ? 0 : 1),
        CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 8, 2),
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left
    };

    internal static Button Button(UIElement content, bool primary = false, string? tooltip = null)
    {
        var button = new Button
        {
            Content = content, FontFamily = Typeface, FontSize = 14,
            Background = Brush(primary ? Primary : "#00000000"), Foreground = Brush(primary ? "#FFFFFF" : Slate500),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
            Padding = primary ? new Thickness(16, 8, 16, 8) : new Thickness(6),
            MinWidth = 0, MinHeight = 0, VerticalAlignment = VerticalAlignment.Center,
            UseSystemFocusVisuals = true
        };
        button.Resources["ButtonBackgroundPointerOver"] = Brush(primary ? "#1D4ED8" : "#F1F5F9");
        button.Resources["ButtonBackgroundPressed"] = Brush(primary ? "#1E40AF" : Slate200);
        button.Resources["ButtonForegroundPointerOver"] = Brush(primary ? "#FFFFFF" : Slate900);
        button.Resources["ButtonBorderBrushPointerOver"] = Brush("#00000000");
        if (tooltip is not null)
        {
            ToolTipService.SetToolTip(button, tooltip);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tooltip);
        }
        return button;
    }

    // Locally authored 24-unit line icons; no web fonts or platform-specific glyphs.
    internal static FrameworkElement Icon(string symbol, string color = Slate500, double size = 20)
    {
        double[][] lines = symbol switch
        {
            "folder" => [[3, 6, 10, 6, 12, 9, 21, 9, 21, 21, 3, 21, 3, 6]],
            "phone" => [[7, 2, 17, 2, 18, 3, 18, 21, 17, 22, 7, 22, 6, 21, 6, 3, 7, 2], [10, 18, 14, 18]],
            "tablet" => [[4, 2, 20, 2, 21, 3, 21, 21, 20, 22, 4, 22, 3, 21, 3, 3, 4, 2], [11, 18, 13, 18]],
            "laptop" => [[4, 4, 20, 4, 20, 17, 4, 17, 4, 4], [4, 17, 1, 21, 23, 21, 20, 17]],
            "headphones" => [[3, 13, 3, 9, 5, 4, 9, 2, 15, 2, 19, 4, 21, 9, 21, 13], [3, 11, 7, 11, 7, 20, 3, 20, 3, 11], [17, 11, 21, 11, 21, 20, 17, 20, 17, 11]],
            "watch" => [[7, 6, 17, 6, 19, 8, 19, 16, 17, 18, 7, 18, 5, 16, 5, 8, 7, 6], [9, 2, 15, 2, 15, 6, 9, 6, 9, 2], [9, 18, 15, 18, 15, 22, 9, 22, 9, 18]],
            "speaker" => [[6, 4, 18, 4, 20, 6, 20, 18, 18, 20, 6, 20, 4, 18, 4, 6, 6, 4], [9, 9, 15, 15], [15, 9, 9, 15]],
            "videocam" => [[3, 6, 15, 6, 15, 18, 3, 18, 3, 6], [15, 10, 21, 7, 21, 17, 15, 14]],
            "memory" => [[3, 5, 21, 5, 21, 19, 3, 19, 3, 5], [7, 5, 7, 19], [11, 5, 11, 19], [15, 5, 15, 19], [19, 5, 19, 19]],
            "cable" => [[5, 4, 9, 4, 9, 10, 15, 10, 15, 20, 19, 20], [5, 4, 3, 6, 5, 8], [19, 20, 21, 18, 19, 16]],
            "monitor" => [[3, 4, 21, 4, 21, 17, 3, 17, 3, 4], [8, 21, 16, 21], [12, 17, 12, 21]],
            "keyboard" => [[2, 7, 22, 7, 22, 17, 2, 17, 2, 7], [5, 11, 7, 11], [9, 11, 11, 11], [13, 11, 15, 11], [17, 11, 19, 11]],
            "mouse" => [[8, 3, 16, 3, 19, 6, 19, 13, 16, 21, 8, 21, 5, 13, 5, 6, 8, 3], [12, 3, 12, 8]],
            "gamepad" => [[5, 8, 19, 8, 22, 12, 20, 19, 16, 17, 8, 17, 4, 19, 2, 12, 5, 8], [7, 12, 7, 15], [5, 13, 9, 13]],
            "camera" => [[3, 8, 21, 8, 21, 19, 3, 19, 3, 8], [8, 8, 10, 5, 15, 5, 17, 8], [12, 13, 12, 14]],
            "router" => [[4, 9, 20, 9, 20, 18, 4, 18, 4, 9], [12, 9, 12, 3], [8, 5, 12, 3, 16, 5], [8, 14, 8, 14], [12, 14, 12, 14], [16, 14, 16, 14]],
            "battery" => [[6, 5, 18, 5, 18, 19, 6, 19, 6, 5], [9, 3, 15, 3], [9, 12, 15, 12], [12, 9, 12, 15]],
            "add" => [[12, 4, 12, 20], [4, 12, 20, 12]],
            "search" => [[15, 16, 21, 22], [17, 10, 15, 15, 10, 17, 5, 15, 3, 10, 5, 5, 10, 3, 15, 5, 17, 10]],
            "edit" => [[4, 16, 16, 4, 20, 8, 8, 20, 3, 21, 4, 16], [13, 7, 17, 11]],
            "delete" => [[4, 6, 20, 6], [9, 6, 9, 3, 15, 3, 15, 6], [6, 6, 7, 21, 17, 21, 18, 6], [10, 10, 10, 17], [14, 10, 14, 17]],
            "delete_outline" => [[4, 6, 20, 6], [9, 6, 9, 3, 15, 3, 15, 6], [6, 6, 7, 21, 17, 21, 18, 6], [10, 10, 10, 17], [14, 10, 14, 17]],
            "delete_forever" => [[4, 6, 20, 6], [9, 6, 9, 3, 15, 3, 15, 6], [6, 6, 7, 21, 17, 21, 18, 6], [10, 10, 10, 17], [14, 10, 14, 17], [12, 8, 12, 19]],
            "warning" => [[12, 3, 21, 20, 3, 20, 12, 3], [12, 9, 12, 14], [12, 17, 12, 17]],
            "check_circle" => [[12, 2, 19, 5, 22, 12, 19, 19, 12, 22, 5, 19, 2, 12, 5, 5, 12, 2], [8, 12, 11, 15, 17, 9]],
            "left" => [[15, 5, 8, 12, 15, 19]],
            "right" => [[9, 5, 16, 12, 9, 19]],
            "close" => [[6, 6, 18, 18], [18, 6, 6, 18]],
            "edit_note" => [[4, 4, 18, 4, 18, 12], [4, 9, 12, 9], [4, 14, 9, 14], [13, 15, 19, 9, 21, 11, 15, 17, 12, 18, 13, 15]],
            "inventory" => [[4, 4, 20, 4, 20, 20, 4, 20, 4, 4], [4, 9, 20, 9], [4, 14, 20, 14], [8, 4, 8, 20], [16, 4, 16, 20]],
            "lock" => [[6, 10, 6, 7, 8, 4, 12, 3, 16, 4, 18, 7, 18, 10], [4, 10, 20, 10, 20, 21, 4, 21, 4, 10], [12, 14, 12, 17]],
            "qr_code" => [[3, 3, 9, 3, 9, 9, 3, 9, 3, 3], [15, 3, 21, 3, 21, 9, 15, 9, 15, 3], [3, 15, 9, 15, 9, 21, 3, 21, 3, 15], [15, 15, 18, 15, 18, 18, 21, 18, 21, 21, 15, 21], [12, 12, 12, 12]],
            "visibility" => [[2, 12, 6, 7, 9, 5, 12, 4, 15, 5, 18, 7, 22, 12, 18, 17, 15, 19, 12, 20, 9, 19, 6, 17, 2, 12], [12, 9, 12, 12]],
            "info" => [[12, 7, 12, 8], [12, 11, 12, 17], [12, 2, 19, 5, 22, 12, 19, 19, 12, 22, 5, 19, 2, 12, 5, 5, 12, 2]],
            "account_tree" => [[12, 4, 12, 8], [6, 8, 18, 8], [6, 8, 6, 12], [18, 8, 18, 12], [3, 12, 9, 12, 9, 18, 3, 18, 3, 12], [15, 12, 21, 12, 21, 18, 15, 18, 15, 12]],
            "tune" => [[4, 6, 20, 6], [4, 12, 20, 12], [4, 18, 20, 18], [8, 4, 8, 8], [15, 10, 15, 14], [11, 16, 11, 20]],
            "expand_more" => [[6, 9, 12, 15, 18, 9]],
            "sync" => [[21, 12, 20, 16, 16, 19, 12, 20, 7, 18, 4, 14], [3, 12, 4, 8, 8, 5, 12, 4, 17, 6, 20, 10], [21, 7, 21, 12, 16, 12], [3, 17, 3, 12, 8, 12]],
            "folder_add" => [[3, 6, 10, 6, 12, 9, 21, 9, 21, 21, 3, 21, 3, 6], [12, 12, 12, 18], [9, 15, 15, 15]],
            _ => [[3, 6, 10, 6, 12, 9, 21, 9, 21, 21, 3, 21, 3, 6]]
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        foreach (var points in lines)
        {
            var figure = new PathFigure { StartPoint = new Point(points[0], points[1]), IsFilled = false };
            for (var i = 2; i < points.Length; i += 2)
                figure.Segments.Add(new LineSegment { Point = new Point(points[i], points[i + 1]) });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            canvas.Children.Add(new Path
            {
                Data = geometry, Stroke = Brush(color), StrokeThickness = 1.75,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            });
        }
        return new Viewbox { Width = size, Height = size, Child = canvas, VerticalAlignment = VerticalAlignment.Center };
    }
}

/// <summary>View-only reflow corresponding to the HTML's sm:flex-row breakpoint.</summary>
internal sealed class ResponsivePair : Grid
{
    private readonly FrameworkElement _trailing;
    private bool? _stacked;

    internal ResponsivePair(FrameworkElement leading, FrameworkElement trailing)
    {
        _trailing = trailing;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Children.Add(leading);
        Children.Add(trailing);
        SetColumn(trailing, 1);
        SizeChanged += (_, args) => Reflow(args.NewSize.Width < 640);
    }

    private void Reflow(bool stacked)
    {
        if (_stacked == stacked) return;
        _stacked = stacked;
        SetColumn(_trailing, stacked ? 0 : 1);
        SetRow(_trailing, stacked ? 1 : 0);
        SetColumnSpan((FrameworkElement)Children[0], stacked ? 2 : 1);
        SetColumnSpan(_trailing, stacked ? 2 : 1);
        _trailing.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _trailing.Margin = stacked ? new Thickness(0, 12, 0, 0) : new Thickness(12, 0, 0, 0);
    }
}
