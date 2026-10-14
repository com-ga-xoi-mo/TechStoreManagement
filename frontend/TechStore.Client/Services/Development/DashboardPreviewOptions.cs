using Microsoft.Extensions.Configuration;

namespace TechStore.Client.Services.Development;

public sealed record DashboardPreviewOptions(bool Enabled)
{
    public const string EnvironmentVariable = "TECHSTORE_DASHBOARD_DEMO";

    public static DashboardPreviewOptions FromConfiguration(IConfiguration configuration) =>
        Parse(configuration[EnvironmentVariable]);

    public static DashboardPreviewOptions Parse(string? value) => value switch
    {
        null or "0" => new(false),
        "1" => new(true),
        _ => throw new ArgumentException($"{EnvironmentVariable} must be 0 or 1.", nameof(value))
    };
}
