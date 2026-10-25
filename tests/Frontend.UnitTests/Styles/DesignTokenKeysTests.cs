using FluentAssertions;
using TechStore.Client.Styles;

namespace TechStore.Client.UnitTests.Styles;

/// <summary>
/// Snapshot of resource keys extracted from the legacy Styles/*.xaml files (Colors, Typography, Metrics,
/// Icons, ControlStyles) before they were removed. XAML pages reference these names, so none may disappear.
/// </summary>
public sealed class DesignTokenKeysTests
{
    private static readonly string[] ExpectedColorKeys =
    [
        "TechStoreCanvasBrush",
        "TechStoreSurfaceBrush",
        "TechStoreSubtleBrush",
        "TechStoreTextBrush",
        "TechStoreSecondaryTextBrush",
        "TechStoreStrokeBrush",
        "TechStoreInputStrokeBrush",
        "TechStorePrimaryBrush",
        "TechStorePrimaryHoverBrush",
        "TechStorePrimaryPressedBrush",
        "TechStoreOnPrimaryBrush",
        "TechStoreFocusBrush",
        "TechStoreNeutralTextBrush",
        "TechStoreNeutralFillBrush",
        "TechStoreNeutralStrokeBrush",
        "TechStoreInfoTextBrush",
        "TechStoreInfoFillBrush",
        "TechStoreInfoStrokeBrush",
        "TechStoreEditContextFillBrush",
        "TechStoreEditContextStrokeBrush",
        "TechStoreSuccessTextBrush",
        "TechStoreSuccessFillBrush",
        "TechStoreSuccessStrokeBrush",
        "TechStoreWarningTextBrush",
        "TechStoreWarningFillBrush",
        "TechStoreWarningStrokeBrush",
        "TechStoreDangerTextBrush",
        "TechStoreDangerFillBrush",
        "TechStoreDangerStrokeBrush",
        "TechStoreAccentTextBrush",
        "TechStoreAccentFillBrush",
        "TechStoreAccentStrokeBrush",
    ];

    private static readonly string[] ExpectedTypographyKeys =
    [
        "TechStoreRegularFont",
        "TechStoreMediumFont",
        "TechStoreSemiBoldFont",
        "TechStoreBoldFont",
        "TechStoreMonoFont",
        "TechStoreBodyTextStyle",
        "TechStoreHeadingTextStyle",
        "TechStoreSectionTextStyle",
        "TechStoreCaptionTextStyle",
        "TechStoreMetricTextStyle",
        "TechStoreMonoTextStyle",
    ];

    private static readonly string[] ExpectedMetricKeys =
    [
        "TechStoreSpaceSmall",
        "TechStoreSpaceMedium",
        "TechStoreSpaceLarge",
        "TechStoreControlMinHeight",
        "TechStoreRowMinHeight",
        "TechStorePanelRadius",
        "TechStoreCardRadius",
        "TechStoreControlRadius",
        "TechStoreBadgeRadius",
        "TechStorePanelPadding",
        "TechStoreBadgePadding",
        "TechStoreStrokeThickness",
    ];

    private static readonly string[] ExpectedIconKeys =
    [
        "TechStoreMoneyIcon",
        "TechStoreBoxIcon",
        "TechStoreWarningIcon",
        "TechStoreBagIcon",
        "TechStoreEmptyIcon",
    ];

    private static readonly string[] ExpectedControlStyleKeys =
    [
        "TechStorePanelStyle",
        "TechStoreButtonStyle",
        "TechStorePrimaryButtonStyle",
        "TechStoreDangerButtonStyle",
        "TechStoreQuietButtonStyle",
        "TechStoreTextBoxStyle",
        "TechStoreAutoSuggestBoxStyle",
        "TechStoreComboBoxStyle",
        "TechStoreFieldErrorStyle",
        "TechStoreTableRowStyle",
        "TechStoreTableHeaderStyle",
    ];

    [Fact]
    public void AppColors_Keys_MatchLegacyColorsXamlSnapshot()
    {
        // Arrange
        var actual = AppColors.Keys;

        // Act
        var missing = ExpectedColorKeys.Except(actual).ToArray();
        var extra = actual.Except(ExpectedColorKeys).ToArray();

        // Assert
        missing.Should().BeEmpty();
        extra.Should().BeEmpty();
        actual.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AppTypography_Keys_MatchLegacyTypographyXamlSnapshot()
    {
        // Arrange / Act
        var actual = AppTypography.Keys;

        // Assert
        actual.Should().BeEquivalentTo(ExpectedTypographyKeys);
        AppTypography.FontFamilies.Keys.Should().BeEquivalentTo(ExpectedTypographyKeys.Where(x => x.EndsWith("Font")));
    }

    [Fact]
    public void AppMetrics_Keys_MatchLegacyMetricsXamlSnapshot()
    {
        // Arrange / Act
        var valueKeys = AppMetrics.Doubles.Keys.Concat(AppMetrics.CornerRadii.Keys).Concat(AppMetrics.Thicknesses.Keys);

        // Assert
        AppMetrics.Keys.Should().BeEquivalentTo(ExpectedMetricKeys);
        valueKeys.Should().BeEquivalentTo(ExpectedMetricKeys);
    }

    [Fact]
    public void AppIcons_Keys_MatchLegacyIconsXamlSnapshot()
    {
        // Arrange / Act
        var actual = AppIcons.Keys;

        // Assert
        actual.Should().BeEquivalentTo(ExpectedIconKeys);
        AppIcons.PathData.Keys.Should().BeEquivalentTo(ExpectedIconKeys);
        AppIcons.PathData.Values.Should().OnlyContain(path => path.StartsWith('M'));
    }

    [Fact]
    public void AppControlStyles_Keys_MatchLegacyControlStylesXamlSnapshot()
    {
        // Arrange / Act
        var actual = AppControlStyles.Keys;

        // Assert
        actual.Should().BeEquivalentTo(ExpectedControlStyleKeys);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrast")]
    public void AppColors_Theme_ContainsEveryColorKey(string theme)
    {
        // Arrange
        var table = Theme(theme);

        // Act
        var missing = AppColors.Keys.Except(table.Keys).ToArray();
        var extra = table.Keys.Except(AppColors.Keys).ToArray();

        // Assert
        missing.Should().BeEmpty($"every color key must exist in {theme}");
        extra.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void AppColors_LightAndDark_ValuesAreHexColors(string theme)
    {
        // Arrange / Act
        var values = Theme(theme).Values;

        // Assert
        values.Should().OnlyContain(v => System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9A-F]{6}$"));
    }

    [Fact]
    public void AppColors_HighContrast_ReferencesOnlySystemColorBrushes()
    {
        // Arrange / Act
        var values = AppColors.HighContrast.Values;

        // Assert
        values.Should().OnlyContain(v => v.StartsWith("SystemColor") && v.EndsWith("ColorBrush"));
    }

    [Fact]
    public void AppColors_Light_UsesStitchPrimaryAndFocus()
    {
        // Arrange / Act
        var light = AppColors.Light;

        // Assert
        light[AppColors.Primary].Should().Be("#0066FF");
        light[AppColors.PrimaryHover].Should().Be("#0052CC");
        light[AppColors.PrimaryPressed].Should().Be("#0040A8");
        light[AppColors.Focus].Should().Be("#0066FF");
        light[AppColors.Canvas].Should().Be("#F8FAFC");
        light[AppColors.Stroke].Should().Be("#E2E8F0");
    }

    private static IReadOnlyDictionary<string, string> Theme(string name) => name switch
    {
        "Light" => AppColors.Light,
        "Dark" => AppColors.Dark,
        _ => AppColors.HighContrast
    };
}
