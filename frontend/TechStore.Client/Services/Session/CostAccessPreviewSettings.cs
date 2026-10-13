using System;

namespace TechStore.Client.Services.Session;

public enum CostAccessPreviewMode
{
    Denied,
    Authorized
}

/// <summary>
/// Selects a presentation-only cost access fixture for explicit development preview.
/// It does not grant server authorization or represent a real user role.
/// </summary>
public static class CostAccessPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_COST_PREVIEW_ACCESS";

    public static CostAccessPreviewMode CurrentMode
    {
        get
        {
#if DEBUG
            return Parse(Environment.GetEnvironmentVariable(EnvironmentVariable));
#else
            return CostAccessPreviewMode.Denied;
#endif
        }
    }

    public static CostAccessPreviewMode Parse(string? value) =>
        string.Equals(value?.Trim(), "authorized", StringComparison.OrdinalIgnoreCase)
            ? CostAccessPreviewMode.Authorized
            : CostAccessPreviewMode.Denied;
}
