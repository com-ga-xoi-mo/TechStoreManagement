using System;

namespace TechStore.Client.Repositories.Catalog;

public static class ProductEditPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_PRODUCT_EDIT_PREVIEW";

    public static ProductEditPreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static ProductEditPreviewScenario Parse(string? value) =>
        Enum.TryParse<ProductEditPreviewScenario>(value?.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : ProductEditPreviewScenario.Success;
}
