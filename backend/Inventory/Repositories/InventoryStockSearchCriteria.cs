namespace TechStore.Api.Inventory.Repositories;

public sealed record InventoryStockSearchCriteria(
    string? Search,
    string? CategoryName,
    Guid? ProductId,
    Guid? VariantId,
    bool? LowStock,
    bool? OutOfStock,
    bool? InStock,
    int Page,
    int PageSize);
