using System;

namespace TechStore.Client.Models.Inventory;

public enum StockAdjustmentDirection
{
    Increase,
    Decrease
}

public enum StockAdjustmentOutcome
{
    Succeeded,
    Conflict,
    Failed
}

public sealed record StockAdjustmentRequest
{
    public StockBalanceKey Key { get; }
    public StockAdjustmentDirection Direction { get; }
    public int Quantity { get; }
    public string Reason { get; }

    public StockAdjustmentRequest(StockBalanceKey key, StockAdjustmentDirection direction, int quantity, string reason)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Adjustment quantity must be positive.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        Direction = direction;
        Quantity = quantity;
        Reason = reason.Trim();
    }
}

public sealed record StockAdjustmentResult(
    StockAdjustmentOutcome Outcome,
    StockBalance? Balance,
    string? Message,
    bool IsPreviewData)
{
    public static StockAdjustmentResult Success(StockBalance balance, bool isPreviewData) =>
        new(StockAdjustmentOutcome.Succeeded, balance ?? throw new ArgumentNullException(nameof(balance)), null, isPreviewData);

    public static StockAdjustmentResult Conflict(string? message = null, bool isPreviewData = false) =>
        new(StockAdjustmentOutcome.Conflict, null, message, isPreviewData);

    public static StockAdjustmentResult Failure(string? message = null, bool isPreviewData = false) =>
        new(StockAdjustmentOutcome.Failed, null, message, isPreviewData);
}
