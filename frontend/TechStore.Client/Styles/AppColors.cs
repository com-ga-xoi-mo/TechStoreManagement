namespace TechStore.Client.Styles;

/// <summary>Color token keys and per-theme values. Pure C# (no WinUI types) so it can be tested headless.</summary>
public static partial class AppColors
{
    public const string Canvas = "TechStoreCanvasBrush";
    public const string Surface = "TechStoreSurfaceBrush";
    public const string Subtle = "TechStoreSubtleBrush";
    public const string Text = "TechStoreTextBrush";
    public const string SecondaryText = "TechStoreSecondaryTextBrush";
    public const string Stroke = "TechStoreStrokeBrush";
    public const string InputStroke = "TechStoreInputStrokeBrush";
    public const string Primary = "TechStorePrimaryBrush";
    public const string PrimaryHover = "TechStorePrimaryHoverBrush";
    public const string PrimaryPressed = "TechStorePrimaryPressedBrush";
    public const string OnPrimary = "TechStoreOnPrimaryBrush";
    public const string Focus = "TechStoreFocusBrush";
    public const string NeutralText = "TechStoreNeutralTextBrush";
    public const string NeutralFill = "TechStoreNeutralFillBrush";
    public const string NeutralStroke = "TechStoreNeutralStrokeBrush";
    public const string InfoText = "TechStoreInfoTextBrush";
    public const string InfoFill = "TechStoreInfoFillBrush";
    public const string InfoStroke = "TechStoreInfoStrokeBrush";
    public const string EditContextFill = "TechStoreEditContextFillBrush";
    public const string EditContextStroke = "TechStoreEditContextStrokeBrush";
    public const string SuccessText = "TechStoreSuccessTextBrush";
    public const string SuccessFill = "TechStoreSuccessFillBrush";
    public const string SuccessStroke = "TechStoreSuccessStrokeBrush";
    public const string WarningText = "TechStoreWarningTextBrush";
    public const string WarningFill = "TechStoreWarningFillBrush";
    public const string WarningStroke = "TechStoreWarningStrokeBrush";
    public const string DangerText = "TechStoreDangerTextBrush";
    public const string DangerFill = "TechStoreDangerFillBrush";
    public const string DangerStroke = "TechStoreDangerStrokeBrush";
    public const string AccentText = "TechStoreAccentTextBrush";
    public const string AccentFill = "TechStoreAccentFillBrush";
    public const string AccentStroke = "TechStoreAccentStrokeBrush";

    public static IReadOnlyList<string> Keys { get; } =
    [
        Canvas,
        Surface,
        Subtle,
        Text,
        SecondaryText,
        Stroke,
        InputStroke,
        Primary,
        PrimaryHover,
        PrimaryPressed,
        OnPrimary,
        Focus,
        NeutralText,
        NeutralFill,
        NeutralStroke,
        InfoText,
        InfoFill,
        InfoStroke,
        EditContextFill,
        EditContextStroke,
        SuccessText,
        SuccessFill,
        SuccessStroke,
        WarningText,
        WarningFill,
        WarningStroke,
        DangerText,
        DangerFill,
        DangerStroke,
        AccentText,
        AccentFill,
        AccentStroke,
    ];

    /// <summary>Light theme: key to #RRGGBB.</summary>
    public static IReadOnlyDictionary<string, string> Light { get; } = new Dictionary<string, string>
    {
        [Canvas] = "#F8FAFC",
        [Surface] = "#FFFFFF",
        [Subtle] = "#F1F5F9",
        [Text] = "#0F172A",
        [SecondaryText] = "#475569",
        [Stroke] = "#E2E8F0",
        [InputStroke] = "#64748B",
        [Primary] = "#0066FF",
        [PrimaryHover] = "#0052CC",
        [PrimaryPressed] = "#0040A8",
        [OnPrimary] = "#FFFFFF",
        [Focus] = "#0066FF",
        [NeutralText] = "#475569",
        [NeutralFill] = "#F1F5F9",
        [NeutralStroke] = "#CBD5E1",
        [InfoText] = "#1D4ED8",
        [InfoFill] = "#EFF6FF",
        [InfoStroke] = "#BFDBFE",
        [EditContextFill] = "#F0F7FF",
        [EditContextStroke] = "#BAE0FF",
        [SuccessText] = "#047857",
        [SuccessFill] = "#ECFDF5",
        [SuccessStroke] = "#A7F3D0",
        [WarningText] = "#B45309",
        [WarningFill] = "#FFFBEB",
        [WarningStroke] = "#FDE68A",
        // Bright red action/text token used by destructive dialogs and inline
        // warnings (matches the inventory delete design's red-500 treatment).
        [DangerText] = "#EF4444",
        [DangerFill] = "#FEF2F2",
        [DangerStroke] = "#FECACA",
        [AccentText] = "#6D28D9",
        [AccentFill] = "#F5F3FF",
        [AccentStroke] = "#DDD6FE",
    };

    /// <summary>Dark theme: key to #RRGGBB.</summary>
    public static IReadOnlyDictionary<string, string> Dark { get; } = new Dictionary<string, string>
    {
        [Canvas] = "#0F172A",
        [Surface] = "#1E293B",
        [Subtle] = "#334155",
        [Text] = "#F8FAFC",
        [SecondaryText] = "#CBD5E1",
        [Stroke] = "#475569",
        [InputStroke] = "#94A3B8",
        [Primary] = "#2563EB",
        [PrimaryHover] = "#1D4ED8",
        [PrimaryPressed] = "#1E40AF",
        [OnPrimary] = "#FFFFFF",
        [Focus] = "#93C5FD",
        [NeutralText] = "#E2E8F0",
        [NeutralFill] = "#334155",
        [NeutralStroke] = "#64748B",
        [InfoText] = "#BFDBFE",
        [InfoFill] = "#172554",
        [InfoStroke] = "#3B82F6",
        [EditContextFill] = "#172554",
        [EditContextStroke] = "#3B82F6",
        [SuccessText] = "#6EE7B7",
        [SuccessFill] = "#064E3B",
        [SuccessStroke] = "#10B981",
        [WarningText] = "#FCD34D",
        [WarningFill] = "#451A03",
        [WarningStroke] = "#D97706",
        [DangerText] = "#FCA5A5",
        [DangerFill] = "#450A0A",
        [DangerStroke] = "#EF4444",
        [AccentText] = "#DDD6FE",
        [AccentFill] = "#2E1065",
        [AccentStroke] = "#8B5CF6",
    };

    /// <summary>HighContrast: key to the name of the system color brush it references.</summary>
    public static IReadOnlyDictionary<string, string> HighContrast { get; } = new Dictionary<string, string>
    {
        [Canvas] = "SystemColorWindowColorBrush",
        [Surface] = "SystemColorWindowColorBrush",
        [Subtle] = "SystemColorWindowColorBrush",
        [Text] = "SystemColorWindowTextColorBrush",
        [SecondaryText] = "SystemColorWindowTextColorBrush",
        [Stroke] = "SystemColorWindowTextColorBrush",
        [InputStroke] = "SystemColorWindowTextColorBrush",
        [Primary] = "SystemColorHighlightColorBrush",
        [PrimaryHover] = "SystemColorHighlightColorBrush",
        [PrimaryPressed] = "SystemColorHighlightColorBrush",
        [OnPrimary] = "SystemColorHighlightTextColorBrush",
        [Focus] = "SystemColorHighlightColorBrush",
        [NeutralText] = "SystemColorWindowTextColorBrush",
        [NeutralFill] = "SystemColorWindowColorBrush",
        [NeutralStroke] = "SystemColorWindowTextColorBrush",
        [InfoText] = "SystemColorWindowTextColorBrush",
        [InfoFill] = "SystemColorWindowColorBrush",
        [InfoStroke] = "SystemColorWindowTextColorBrush",
        [EditContextFill] = "SystemColorWindowColorBrush",
        [EditContextStroke] = "SystemColorWindowTextColorBrush",
        [SuccessText] = "SystemColorWindowTextColorBrush",
        [SuccessFill] = "SystemColorWindowColorBrush",
        [SuccessStroke] = "SystemColorWindowTextColorBrush",
        [WarningText] = "SystemColorWindowTextColorBrush",
        [WarningFill] = "SystemColorWindowColorBrush",
        [WarningStroke] = "SystemColorWindowTextColorBrush",
        [DangerText] = "SystemColorWindowTextColorBrush",
        [DangerFill] = "SystemColorWindowColorBrush",
        [DangerStroke] = "SystemColorWindowTextColorBrush",
        [AccentText] = "SystemColorWindowTextColorBrush",
        [AccentFill] = "SystemColorWindowColorBrush",
        [AccentStroke] = "SystemColorWindowTextColorBrush",
    };
}
