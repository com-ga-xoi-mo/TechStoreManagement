using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TechStore.Client.Styles;

/// <summary>
/// Source-compatible facade for older C# pages. Values come from the canonical AppColors
/// palette so this class no longer maintains a second copy of the design tokens.
/// Brush properties remain snapshots; new UI should use AppColors.Brushes for live theme updates.
/// </summary>
public static class StitchTokens
{
    public static readonly Color Primary = From(AppColors.Light, AppColors.Primary);
    public static readonly Color PrimaryHover = From(AppColors.Light, AppColors.PrimaryHover);
    public static readonly Color PrimaryPressed = From(AppColors.Light, AppColors.PrimaryPressed);
    public static readonly Color PrimaryLight = From(AppColors.Light, AppColors.InfoFill);

    public static readonly Color SurfaceCanvas = From(AppColors.Light, AppColors.Canvas);
    public static readonly Color SurfaceWhite = From(AppColors.Light, AppColors.Surface);
    public static readonly Color SurfaceBorder = From(AppColors.Light, AppColors.Stroke);
    public static readonly Color SurfaceBorderDark = From(AppColors.Dark, AppColors.Stroke);

    public static readonly Color ContentTitle = From(AppColors.Light, AppColors.Text);
    public static readonly Color ContentBody = From(AppColors.Light, AppColors.Text);
    public static readonly Color ContentMuted = From(AppColors.Light, AppColors.SecondaryText);

    public static readonly Color SuccessGreen = From(AppColors.Light, AppColors.SuccessText);
    public static readonly Color SuccessBg = From(AppColors.Light, AppColors.SuccessFill);
    public static readonly Color SuccessBorder = From(AppColors.Light, AppColors.SuccessStroke);
    public static readonly Color SuccessText = From(AppColors.Light, AppColors.SuccessText);

    public static readonly Color ErrorRed = From(AppColors.Light, AppColors.DangerText);
    public static readonly Color ErrorBg = From(AppColors.Light, AppColors.DangerFill);
    public static readonly Color ErrorBorder = From(AppColors.Light, AppColors.DangerStroke);
    public static readonly Color ErrorText = From(AppColors.Light, AppColors.DangerText);

    public static SolidColorBrush PrimaryBrush => new(Primary);
    public static SolidColorBrush SurfaceCanvasBrush => new(SurfaceCanvas);
    public static SolidColorBrush SurfaceWhiteBrush => new(SurfaceWhite);
    public static SolidColorBrush SurfaceBorderBrush => new(SurfaceBorder);
    public static SolidColorBrush ContentTitleBrush => new(ContentTitle);
    public static SolidColorBrush ContentBodyBrush => new(ContentBody);
    public static SolidColorBrush ContentMutedBrush => new(ContentMuted);
    public static SolidColorBrush SuccessBgBrush => new(SuccessBg);
    public static SolidColorBrush SuccessBorderBrush => new(SuccessBorder);
    public static SolidColorBrush SuccessTextBrush => new(SuccessText);
    public static SolidColorBrush ErrorBgBrush => new(ErrorBg);
    public static SolidColorBrush ErrorBorderBrush => new(ErrorBorder);
    public static SolidColorBrush ErrorTextBrush => new(ErrorText);

    private static Color From(IReadOnlyDictionary<string, string> palette, string token)
    {
        var hex = palette[token];
        return Microsoft.UI.ColorHelper.FromArgb(
            0xFF,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));
    }
}
