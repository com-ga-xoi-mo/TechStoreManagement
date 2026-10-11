using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TechStore.Client.Models.Inventory;

public sealed record StockBalanceKey
{
    public string ProductId { get; }
    public string? VariantId { get; }

    public StockBalanceKey(string productId, string? variantId = null)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID is required.", nameof(productId));
        if (variantId is not null && string.IsNullOrWhiteSpace(variantId))
            throw new ArgumentException("Variant ID must be null or non-empty.", nameof(variantId));

        ProductId = productId;
        VariantId = variantId;
    }
}

public enum StockMovementType
{
    Receipt,
    Sale,
    Return,
    Adjustment,
    Transfer
}

public sealed record StockMovement
{
    public string MovementId { get; }
    public DateTimeOffset OccurredAt { get; }
    public StockMovementType Type { get; }
    public int QuantityChange { get; }
    public int PhysicalQuantityAfter { get; }
    public string? Note { get; }

    public StockMovement(
        string movementId,
        DateTimeOffset occurredAt,
        StockMovementType type,
        int quantityChange,
        int physicalQuantityAfter,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(movementId))
            throw new ArgumentException("Movement ID is required.", nameof(movementId));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (quantityChange == 0)
            throw new ArgumentOutOfRangeException(nameof(quantityChange), "A movement must change physical quantity.");
        if (physicalQuantityAfter < 0)
            throw new ArgumentOutOfRangeException(nameof(physicalQuantityAfter));

        MovementId = movementId;
        OccurredAt = occurredAt;
        Type = type;
        QuantityChange = quantityChange;
        PhysicalQuantityAfter = physicalQuantityAfter;
        Note = note;
    }
}

/// <summary>A repository-provided, read-only balance and movement snapshot.</summary>
public sealed record StockDetailSnapshot
{
    public StockBalance Balance { get; }
    public IReadOnlyList<StockMovement> RecentMovements { get; }
    public DateTimeOffset LastRefreshedAt { get; }
    public bool IsPreviewData { get; }

    public StockDetailSnapshot(
        StockBalance balance,
        IEnumerable<StockMovement> recentMovements,
        DateTimeOffset lastRefreshedAt,
        bool isPreviewData)
    {
        Balance = balance ?? throw new ArgumentNullException(nameof(balance));
        ArgumentNullException.ThrowIfNull(recentMovements);

        var movements = recentMovements.ToArray();
        if (movements.Length > 5)
            throw new ArgumentOutOfRangeException(nameof(recentMovements), "At most five recent movements are shown.");
        if (movements.Any(movement => movement is null))
            throw new ArgumentException("Movement entries cannot be null.", nameof(recentMovements));

        RecentMovements = new ReadOnlyCollection<StockMovement>(movements);
        LastRefreshedAt = lastRefreshedAt;
        IsPreviewData = isPreviewData;
    }
}
