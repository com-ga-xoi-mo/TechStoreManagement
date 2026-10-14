using TechStore.Client.Services.Development;
namespace TechStore.Client.UnitTests.Development;

public sealed class GalleryOptionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void Parse_SupportedFlag_SelectsExpectedLaunch(string? input, bool expected)
    {
        Assert.Equal(expected, GalleryOptions.Parse(input).Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("1 ")]
    [InlineData("secret-sentinel")]
    public void Parse_InvalidFlag_FailsWithoutEchoingInput(string input)
    {
        var exception = Assert.Throws<ArgumentException>(() => GalleryOptions.Parse(input));
        Assert.Contains("TECHSTORE_UI_GALLERY", exception.Message);
        if (input == "secret-sentinel") Assert.DoesNotContain(input, exception.Message);
    }

    [Fact]
    public void StartupDiagnostic_ReportsInvalidGalleryConfigurationWithoutEchoingValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => GalleryOptions.Parse("secret-sentinel"));

        var diagnostic = FrontendStartupDiagnostics.CreateConfigurationFailureMessage(exception);

        Assert.Contains("Client startup configuration failed", diagnostic);
        Assert.Contains("TECHSTORE_UI_GALLERY", diagnostic);
        Assert.DoesNotContain("secret-sentinel", diagnostic);
    }
}
