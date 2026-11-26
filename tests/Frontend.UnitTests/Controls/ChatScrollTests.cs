using TechStore.Client.Controls.Presentation;

namespace TechStore.Client.UnitTests.Controls;

public sealed class ChatScrollTests
{
    [Theory]
    [InlineData(0, 0, true)]       // nothing to scroll
    [InlineData(500, 500, true)]   // exactly at the bottom
    [InlineData(480, 500, true)]   // within tolerance
    [InlineData(470, 500, false)]  // scrolled up a bit
    [InlineData(0, 500, false)]    // at the top of a long chat
    public void IsAtBottom_UsesTolerance(double offset, double scrollable, bool expected)
    {
        // Act
        var atBottom = ChatScroll.IsAtBottom(offset, scrollable);

        // Assert
        Assert.Equal(expected, atBottom);
        Assert.Equal(!expected, ChatScroll.ShowJumpToLatest(offset, scrollable));
    }
}
