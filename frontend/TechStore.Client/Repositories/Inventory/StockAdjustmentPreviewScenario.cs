using System;

namespace TechStore.Client.Repositories.Inventory;

public enum StockAdjustmentPreviewScenario
{
    Success,
    Conflict,
    Error,
    Delayed
}

public static class StockAdjustmentPreviewSettings
{
    public const string EnvironmentVariable = "TECHSTORE_STOCK_ADJUSTMENT_PREVIEW";

    public static StockAdjustmentPreviewScenario CurrentScenario => Parse(
        Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static StockAdjustmentPreviewScenario Parse(string? value) =>
        Enum.TryParse<StockAdjustmentPreviewScenario>(value?.Trim(), ignoreCase: true, out var scenario)
            ? scenario
            : StockAdjustmentPreviewScenario.Success;
}
