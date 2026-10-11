using System;

namespace TechStore.Client.Repositories.Inventory;

public static class StockDetailPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_STOCK_DETAIL_PREVIEW";

    public static StockDetailPreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static StockDetailPreviewScenario Parse(string? value) =>
        Enum.TryParse<StockDetailPreviewScenario>(value?.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : StockDetailPreviewScenario.Populated;
}
