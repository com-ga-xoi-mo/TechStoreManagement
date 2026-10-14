using Microsoft.Extensions.Configuration;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Configuration;

public sealed class ApiClientOptionsTests
{
    [Fact]
    public void FromConfiguration_UsesDevelopmentDefaults()
    {
        // Arrange
        var configuration = new ConfigurationManager();

        // Act
        var options = ApiClientOptions.FromConfiguration(configuration);

        // Assert
        Assert.Equal("http://localhost:5176/", options.Validate().AbsoluteUri);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void FromConfiguration_UsesEnvironmentOverridesAndNormalizesBaseUrl()
    {
        // Arrange
        var configuration = new ConfigurationManager
        {
            [ApiClientOptions.BaseUrlEnvironmentVariable] = "https://localhost:7207/api",
            [ApiClientOptions.TimeoutEnvironmentVariable] = "12.5"
        };

        // Act
        var options = ApiClientOptions.FromConfiguration(configuration);

        // Assert
        Assert.Equal("https://localhost:7207/api/", options.Validate().AbsoluteUri);
        Assert.Equal(12.5, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("relative/api")]
    [InlineData("ftp://localhost/api")]
    [InlineData("https://user:password@localhost/api")]
    [InlineData("https://localhost/api?secret=value")]
    [InlineData("https://localhost/api#fragment")]
    public void Validate_RejectsInvalidBaseUrls(string baseUrl)
    {
        // Arrange
        var options = new ApiClientOptions { BaseUrl = baseUrl };

        // Act
        var error = Assert.Throws<InvalidOperationException>(() => options.Validate());

        // Assert
        Assert.Contains(ApiClientOptions.BaseUrlEnvironmentVariable, error.Message);
        Assert.DoesNotContain("password", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(3_000_000)]
    public void Validate_RejectsInvalidTimeouts(double timeoutSeconds)
    {
        // Arrange
        var options = new ApiClientOptions { TimeoutSeconds = timeoutSeconds };

        // Act
        var error = Assert.Throws<InvalidOperationException>(() => options.Validate());

        // Assert
        Assert.Contains(ApiClientOptions.TimeoutEnvironmentVariable, error.Message);
    }

    [Fact]
    public void FromConfiguration_RejectsMalformedTimeoutWithoutLoggingItsValue()
    {
        // Arrange
        const string privateValue = "private-secret";
        var configuration = new ConfigurationManager
        {
            [ApiClientOptions.TimeoutEnvironmentVariable] = privateValue
        };

        // Act
        var error = Assert.Throws<InvalidOperationException>(
            () => ApiClientOptions.FromConfiguration(configuration));

        // Assert
        Assert.Contains(ApiClientOptions.TimeoutEnvironmentVariable, error.Message);
        Assert.DoesNotContain(privateValue, error.Message);
    }
}
