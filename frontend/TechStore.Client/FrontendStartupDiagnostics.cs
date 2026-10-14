using Microsoft.Extensions.Logging;

namespace TechStore.Client;

internal static class FrontendStartupDiagnostics
{
    public static void LogConfigurationFailure(ILogger logger, Exception exception) =>
        logger.LogCritical("Client startup configuration failed: {Diagnostic}", exception.Message);

    public static string CreateConfigurationFailureMessage(Exception exception) =>
        $"Client startup configuration failed: {exception.Message}";
}
