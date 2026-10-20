using FluentAssertions;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Styles;

namespace TechStore.Client.UnitTests.Controls;

public sealed class ControlPresentationTests
{
    [Theory]
    [InlineData(true, "", StatCardDisplayState.Loading)]
    [InlineData(true, "Lỗi", StatCardDisplayState.Loading)]
    [InlineData(false, "Lỗi", StatCardDisplayState.Error)]
    [InlineData(false, "  ", StatCardDisplayState.Content)]
    [InlineData(false, null, StatCardDisplayState.Content)]
    public void StatCard_GetState_LoadingBeatsErrorBeatsContent(bool isLoading, string? error, StatCardDisplayState expected)
    {
        // Arrange / Act
        var state = StatCardPresentation.GetState(isLoading, error);

        // Assert
        state.Should().Be(expected);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("482.650.000", "482.650.000")]
    [InlineData("", "—")]
    [InlineData("   ", "—")]
    [InlineData(null, "—")]
    public void StatCard_GetDisplayValue_ZeroIsValueAndMissingIsDash(string? input, string expected)
    {
        // Arrange / Act
        var value = StatCardPresentation.GetDisplayValue(input);

        // Assert
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("₫", " ₫")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StatCard_GetDisplayUnit_PrefixesOneSpaceOnlyWhenPresent(string? input, string expected)
    {
        // Arrange / Act
        var unit = StatCardPresentation.GetDisplayUnit(input);

        // Assert
        unit.Should().Be(expected);
    }

    [Theory]
    [InlineData("Thử lại", true, true)]
    [InlineData("Thử lại", false, false)]
    [InlineData("", true, false)]
    [InlineData(null, true, false)]
    public void EmptyState_ShowAction_RequiresLabelAndCommand(string? text, bool hasCommand, bool expected)
    {
        // Arrange / Act
        var show = EmptyStatePresentation.ShowAction(text, hasCommand);

        // Assert
        show.Should().Be(expected);
    }

    [Theory]
    [InlineData("Đã bán", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    public void StatusBadge_IsVisible_HiddenWhenLabelBlank(string? label, bool expected)
    {
        // Arrange / Act
        var visible = StatusBadgePresentation.IsVisible(label);

        // Assert
        visible.Should().Be(expected);
    }

    [Theory]
    [InlineData(StatusTone.Neutral, "TechStoreNeutralFillBrush", "TechStoreNeutralStrokeBrush", "TechStoreNeutralTextBrush")]
    [InlineData(StatusTone.Info, "TechStoreInfoFillBrush", "TechStoreInfoStrokeBrush", "TechStoreInfoTextBrush")]
    [InlineData(StatusTone.Success, "TechStoreSuccessFillBrush", "TechStoreSuccessStrokeBrush", "TechStoreSuccessTextBrush")]
    [InlineData(StatusTone.Warning, "TechStoreWarningFillBrush", "TechStoreWarningStrokeBrush", "TechStoreWarningTextBrush")]
    [InlineData(StatusTone.Danger, "TechStoreDangerFillBrush", "TechStoreDangerStrokeBrush", "TechStoreDangerTextBrush")]
    [InlineData(StatusTone.Accent, "TechStoreAccentFillBrush", "TechStoreAccentStrokeBrush", "TechStoreAccentTextBrush")]
    public void StatusBadge_GetKeys_MapsToneToFillStrokeTextKeys(StatusTone tone, string fill, string stroke, string text)
    {
        // Arrange / Act
        var keys = StatusBadgePresentation.GetKeys(tone);

        // Assert
        keys.Should().Be(new ToneKeys(fill, stroke, text));
    }

    [Fact]
    public void StatusBadge_GetKeys_UnknownToneFallsBackToNeutral()
    {
        // Arrange
        var unknown = (StatusTone)99;

        // Act
        var keys = StatusBadgePresentation.GetKeys(unknown);

        // Assert
        keys.Should().Be(StatusBadgePresentation.GetKeys(StatusTone.Neutral));
    }

    [Fact]
    public void StatusBadge_GetKeys_EveryToneReturnsKnownColorKeys()
    {
        // Arrange / Act
        var all = Enum.GetValues<StatusTone>().Select(StatusBadgePresentation.GetKeys).ToArray();

        // Assert
        all.SelectMany(k => new[] { k.Fill, k.Stroke, k.Text }).Should().OnlyContain(k => AppColors.Keys.Contains(k));
    }
}
