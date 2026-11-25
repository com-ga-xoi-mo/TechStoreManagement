namespace TechStore.Client.Controls.Presentation;

/// <summary>Scroll rules for the assistant chat.</summary>
public static class ChatScroll
{
    /// <summary>Pixels from the bottom that still count as "at the latest message" (rounding, partial last line).</summary>
    public const double BottomTolerance = 24;

    public static bool IsAtBottom(double verticalOffset, double scrollableHeight) =>
        scrollableHeight - verticalOffset <= BottomTolerance;

    /// <summary>The "↓" button shows only when there is something below the current view.</summary>
    public static bool ShowJumpToLatest(double verticalOffset, double scrollableHeight) =>
        !IsAtBottom(verticalOffset, scrollableHeight);
}
