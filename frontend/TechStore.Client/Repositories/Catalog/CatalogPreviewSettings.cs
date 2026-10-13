using System;

namespace TechStore.Client.Repositories.Catalog;

public static class CatalogPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_CATALOG_PREVIEW";

    public static CatalogPreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static CatalogPreviewScenario Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CatalogPreviewScenario.Populated;
        }

        return Enum.TryParse<CatalogPreviewScenario>(value.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : CatalogPreviewScenario.Populated;
    }
}
