using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public sealed record SupplierSearchCriteria(
    string? Search,
    bool? IsActive,
    int Page,
    int PageSize);

public sealed record SupplierView(
    Supplier Supplier,
    int ReceiptCount);
