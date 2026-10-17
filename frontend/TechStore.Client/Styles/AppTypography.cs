namespace TechStore.Client.Styles;

public static partial class AppTypography
{
    public const string RegularFont = "TechStoreRegularFont";
    public const string MediumFont = "TechStoreMediumFont";
    public const string SemiBoldFont = "TechStoreSemiBoldFont";
    public const string BoldFont = "TechStoreBoldFont";
    public const string MonoFont = "TechStoreMonoFont";
    public const string BodyText = "TechStoreBodyTextStyle";
    public const string HeadingText = "TechStoreHeadingTextStyle";
    public const string SectionText = "TechStoreSectionTextStyle";
    public const string CaptionText = "TechStoreCaptionTextStyle";
    public const string MetricText = "TechStoreMetricTextStyle";
    public const string MonoText = "TechStoreMonoTextStyle";

    public static IReadOnlyDictionary<string, string> FontFamilies { get; } = new Dictionary<string, string>
    {
        [RegularFont] = "ms-appx:///Assets/Fonts/Inter-Regular.ttf#Inter",
        [MediumFont] = "ms-appx:///Assets/Fonts/Inter-Medium.ttf#Inter",
        [SemiBoldFont] = "ms-appx:///Assets/Fonts/Inter-SemiBold.ttf#Inter",
        [BoldFont] = "ms-appx:///Assets/Fonts/Inter-Bold.ttf#Inter",
        [MonoFont] = "ms-appx:///Assets/Fonts/JetBrainsMono-Regular.ttf#JetBrains Mono",
    };

    public static IReadOnlyList<string> Keys { get; } =
    [
        RegularFont,
        MediumFont,
        SemiBoldFont,
        BoldFont,
        MonoFont,
        BodyText,
        HeadingText,
        SectionText,
        CaptionText,
        MetricText,
        MonoText,
    ];
}
