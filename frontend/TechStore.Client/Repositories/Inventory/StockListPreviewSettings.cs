using System;

namespace TechStore.Client.Repositories.Inventory;

public enum StockListPreviewScenario
{
    Populated,
    Empty,
    Delayed,
    Error
}

public static class StockListPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_STOCK_LIST_PREVIEW";

    public static StockListPreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static StockListPreviewScenario Parse(string? value) =>
        Enum.TryParse<StockListPreviewScenario>(value?.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : StockListPreviewScenario.Populated;
}
