using Microsoft.Extensions.Logging;
using TechStore.Client;

namespace TechStore.Client.UnitTests.Composition;

public sealed class StartupDiagnosticsTests
{
    [Fact]
    public void LogConfigurationFailure_RecordsSanitizedDiagnostic()
    {
        // Arrange
        var messages = new List<string>();
        var logger = new RecordingLogger(messages);
        var error = new InvalidOperationException(
            "Invalid API configuration: TECHSTORE_API_BASE_URL must be an absolute HTTP or HTTPS URL.");

        // Act
        FrontendStartupDiagnostics.LogConfigurationFailure(logger, error);
        var output = string.Join('\n', messages);

        // Assert
        Assert.Contains("startup configuration failed", output);
        Assert.Contains("TECHSTORE_API_BASE_URL", output);
        Assert.DoesNotContain("Authorization", output);
        Assert.DoesNotContain("password", output);
    }

    private sealed class RecordingLogger(ICollection<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }
}
