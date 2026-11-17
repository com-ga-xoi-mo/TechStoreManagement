namespace TechStore.Api.Inventory.Services;

public readonly record struct StockLockKey(Guid ProductId, Guid? VariantId);

public static class InventoryLockOrdering
{
    public static IEnumerable<StockLockKey> OrderKeys(IEnumerable<StockLockKey> keys) =>
        keys.OrderBy(k => k.ProductId).ThenBy(k => k.VariantId);

    public static IEnumerable<T> OrderByStockLockKey<T>(
        this IEnumerable<T> source,
        Func<T, Guid> productIdSelector,
        Func<T, Guid?> variantIdSelector) =>
        source
            .OrderBy(productIdSelector)
            .ThenBy(variantIdSelector);
}
