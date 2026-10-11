using System;

namespace TechStore.Client.Repositories.Catalog;

public static class ProductCreatePreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_PRODUCT_CREATE_PREVIEW";

    public static ProductCreatePreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static ProductCreatePreviewScenario Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ProductCreatePreviewScenario.Success;
        }

        return Enum.TryParse<ProductCreatePreviewScenario>(value.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : ProductCreatePreviewScenario.Success;
    }
}
