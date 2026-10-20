using TechStore.Client.Styles;

namespace TechStore.Client.Controls.Presentation;

/// <summary>Which part of a StatCard is shown. Loading wins over error, error wins over content.</summary>
public enum StatCardDisplayState
{
    Content,
    Loading,
    Error
}

/// <summary>Pure presentation rules for StatCard (no WinUI types, testable headless).</summary>
public static class StatCardPresentation
{
    public const string MissingValue = "—";

    public static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public static StatCardDisplayState GetState(bool isLoading, string? errorText) =>
        isLoading ? StatCardDisplayState.Loading
        : HasText(errorText) ? StatCardDisplayState.Error
        : StatCardDisplayState.Content;

    /// <summary>Missing text shows an em dash; "0" is a real value and stays.</summary>
    public static string GetDisplayValue(string? valueText) => HasText(valueText) ? valueText! : MissingValue;

    /// <summary>Unit is rendered after the value with one leading space; empty when absent.</summary>
    public static string GetDisplayUnit(string? unitText) => HasText(unitText) ? " " + unitText : string.Empty;
}

/// <summary>Pure presentation rules for EmptyStateControl.</summary>
public static class EmptyStatePresentation
{
    /// <summary>The action is shown only with both a label and a command.</summary>
    public static bool ShowAction(string? actionText, bool hasCommand) =>
        hasCommand && !string.IsNullOrWhiteSpace(actionText);

    public static bool ShowDescription(string? description) => !string.IsNullOrWhiteSpace(description);
}

/// <summary>Resource keys (Fill, Stroke, Text) for one StatusTone.</summary>
public readonly record struct ToneKeys(string Fill, string Stroke, string Text);

/// <summary>Pure presentation rules for StatusBadge and tone coloring shared with StatCard.</summary>
public static class StatusBadgePresentation
{
    /// <summary>A badge without a label is hidden.</summary>
    public static bool IsVisible(string? label) => !string.IsNullOrWhiteSpace(label);

    /// <summary>Maps a tone to its theme color keys; unknown tones fall back to Neutral.</summary>
    public static ToneKeys GetKeys(StatusTone tone) => (Enum.IsDefined(tone) ? tone : StatusTone.Neutral) switch
    {
        StatusTone.Info => new(AppColors.InfoFill, AppColors.InfoStroke, AppColors.InfoText),
        StatusTone.Success => new(AppColors.SuccessFill, AppColors.SuccessStroke, AppColors.SuccessText),
        StatusTone.Warning => new(AppColors.WarningFill, AppColors.WarningStroke, AppColors.WarningText),
        StatusTone.Danger => new(AppColors.DangerFill, AppColors.DangerStroke, AppColors.DangerText),
        StatusTone.Accent => new(AppColors.AccentFill, AppColors.AccentStroke, AppColors.AccentText),
        _ => new(AppColors.NeutralFill, AppColors.NeutralStroke, AppColors.NeutralText)
    };
}
