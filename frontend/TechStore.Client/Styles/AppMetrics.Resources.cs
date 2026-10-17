using Microsoft.UI.Xaml;

namespace TechStore.Client.Styles;

public static partial class AppMetrics
{
    /// <summary>
    /// Typed metric values for C# Markup (<c>.Padding(AppMetrics.Values.BadgePadding)</c>).
    /// Plain values, not resource references: Markup allows only one StaticResource binding per element,
    /// and metrics do not vary by theme.
    /// </summary>
    public static class Values
    {
        public static double SpaceSmall => AppMetrics.Doubles[AppMetrics.SpaceSmall];
        public static double SpaceMedium => AppMetrics.Doubles[AppMetrics.SpaceMedium];
        public static double SpaceLarge => AppMetrics.Doubles[AppMetrics.SpaceLarge];
        public static double ControlMinHeight => AppMetrics.Doubles[AppMetrics.ControlMinHeight];
        public static double RowMinHeight => AppMetrics.Doubles[AppMetrics.RowMinHeight];
        public static CornerRadius PanelRadius => new(AppMetrics.CornerRadii[AppMetrics.PanelRadius]);
        public static CornerRadius CardRadius => new(AppMetrics.CornerRadii[AppMetrics.CardRadius]);
        public static CornerRadius ControlRadius => new(AppMetrics.CornerRadii[AppMetrics.ControlRadius]);
        public static CornerRadius BadgeRadius => new(AppMetrics.CornerRadii[AppMetrics.BadgeRadius]);
        public static Thickness PanelPadding => new(AppMetrics.Thicknesses[AppMetrics.PanelPadding].Horizontal, AppMetrics.Thicknesses[AppMetrics.PanelPadding].Vertical, AppMetrics.Thicknesses[AppMetrics.PanelPadding].Horizontal, AppMetrics.Thicknesses[AppMetrics.PanelPadding].Vertical);
        public static Thickness BadgePadding => new(AppMetrics.Thicknesses[AppMetrics.BadgePadding].Horizontal, AppMetrics.Thicknesses[AppMetrics.BadgePadding].Vertical, AppMetrics.Thicknesses[AppMetrics.BadgePadding].Horizontal, AppMetrics.Thicknesses[AppMetrics.BadgePadding].Vertical);
        public static Thickness StrokeThickness => new(AppMetrics.Thicknesses[AppMetrics.StrokeThickness].Horizontal, AppMetrics.Thicknesses[AppMetrics.StrokeThickness].Vertical, AppMetrics.Thicknesses[AppMetrics.StrokeThickness].Horizontal, AppMetrics.Thicknesses[AppMetrics.StrokeThickness].Vertical);
    }
}
