namespace TechStore.Client.Styles;

public static partial class AppMetrics
{
    public const string SpaceSmall = "TechStoreSpaceSmall";
    public const string SpaceMedium = "TechStoreSpaceMedium";
    public const string SpaceLarge = "TechStoreSpaceLarge";
    public const string ControlMinHeight = "TechStoreControlMinHeight";
    public const string RowMinHeight = "TechStoreRowMinHeight";
    public const string PanelRadius = "TechStorePanelRadius";
    public const string CardRadius = "TechStoreCardRadius";
    public const string ControlRadius = "TechStoreControlRadius";
    public const string BadgeRadius = "TechStoreBadgeRadius";
    public const string PanelPadding = "TechStorePanelPadding";
    public const string BadgePadding = "TechStoreBadgePadding";
    public const string StrokeThickness = "TechStoreStrokeThickness";

    public static IReadOnlyDictionary<string, double> Doubles { get; } = new Dictionary<string, double>
    {
        [SpaceSmall] = 8,
        [SpaceMedium] = 16,
        [SpaceLarge] = 24,
        [ControlMinHeight] = 36,
        [RowMinHeight] = 44,
    };

    /// <summary>Uniform corner radius values.</summary>
    public static IReadOnlyDictionary<string, double> CornerRadii { get; } = new Dictionary<string, double>
    {
        [PanelRadius] = 12,
        [CardRadius] = 8,
        [ControlRadius] = 8,
        [BadgeRadius] = 4,
    };

    /// <summary>Thickness as (horizontal, vertical), same meaning as the XAML two-value form.</summary>
    public static IReadOnlyDictionary<string, (double Horizontal, double Vertical)> Thicknesses { get; } = new Dictionary<string, (double Horizontal, double Vertical)>
    {
        [PanelPadding] = (20, 20),
        [BadgePadding] = (8, 2),
        [StrokeThickness] = (1, 1),
    };

    public static IReadOnlyList<string> Keys { get; } =
    [
        SpaceSmall,
        SpaceMedium,
        SpaceLarge,
        ControlMinHeight,
        RowMinHeight,
        PanelRadius,
        CardRadius,
        ControlRadius,
        BadgeRadius,
        PanelPadding,
        BadgePadding,
        StrokeThickness,
    ];
}
