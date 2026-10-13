using System;

namespace TechStore.Client.Repositories.Catalog;

public enum ProductDetailPreviewScenario
{
    Populated,
    Delayed,
    Error,
    Unavailable
}

public static class ProductDetailPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_PRODUCT_DETAIL_PREVIEW";

    public static ProductDetailPreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static ProductDetailPreviewScenario Parse(string? value) =>
        Enum.TryParse<ProductDetailPreviewScenario>(value?.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : ProductDetailPreviewScenario.Populated;
}
