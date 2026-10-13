using System;

namespace TechStore.Client.Repositories.Catalog;

public static class CategoryTreePreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_CATEGORY_TREE_PREVIEW";

    public static CategoryTreePreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static CategoryTreePreviewScenario Parse(string? value) =>
        Enum.TryParse<CategoryTreePreviewScenario>(value?.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : CategoryTreePreviewScenario.Populated;
}
