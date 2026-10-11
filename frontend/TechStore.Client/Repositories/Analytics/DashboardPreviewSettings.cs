using System;
using TechStore.Client.Services.Session;

namespace TechStore.Client.Repositories.Analytics;

public static class DashboardPreviewSettings
{
    public static DashboardFixtureScenario CurrentScenario
    {
        get
        {
#if DEBUG
            return Resolve(
                isDebugBuild: true,
                isPreviewMode: SessionConfig.CurrentMode == AuthMode.ExplicitPreview,
                Environment.GetEnvironmentVariable("TECHSTORE_DASHBOARD_SCENARIO"));
#else
            return DashboardFixtureScenario.Populated;
#endif
        }
    }

    public static DashboardFixtureScenario Resolve(bool isDebugBuild, bool isPreviewMode, string? scenario)
    {
        if (!isDebugBuild || !isPreviewMode)
        {
            return DashboardFixtureScenario.Populated;
        }

        return scenario?.Trim().ToLowerInvariant() switch
        {
            "empty" => DashboardFixtureScenario.Empty,
            "delayed" => DashboardFixtureScenario.Delayed,
            "error" => DashboardFixtureScenario.Error,
            "onepoint" => DashboardFixtureScenario.OnePoint,
            _ => DashboardFixtureScenario.Populated
        };
    }
}
