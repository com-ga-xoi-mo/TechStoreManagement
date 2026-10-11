namespace TechStore.Api.Inventory.Repositories;

public sealed record InventoryStockSearchCriteria(
    Guid? ProductId,
    Guid? VariantId,
    bool? LowStock,
    int Page,
    int PageSize);
