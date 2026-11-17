namespace TechStore.Client.Models.Inventory;

public sealed record SupplierItem(
    Guid Id,
    string Name,
    string? ContactName,
    string Phone,
    string? Email,
    string? Address,
    bool IsActive,
    int ReceiptCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
