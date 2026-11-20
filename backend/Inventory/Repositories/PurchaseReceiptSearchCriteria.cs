namespace TechStore.Api.Inventory.Repositories;

public sealed record PurchaseReceiptSearchCriteria(
    string? Search,
    Guid? SupplierId,
    int Page,
    int PageSize);
