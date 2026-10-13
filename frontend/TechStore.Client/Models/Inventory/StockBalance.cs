using System;

namespace TechStore.Client.Models.Inventory;

public enum InventoryStockStatus
{
    All,
    Available,
    LowStock,
    OutOfStock
}

/// <summary>A read-only product or variant balance shown in the inventory preview.</summary>
public sealed record StockBalance
{
    public string ProductId { get; }
    public string ProductName { get; }
    public string ProductSku { get; }
    public string? VariantId { get; }
    public string? VariantName { get; }
    public string? VariantSku { get; }
    public string CategoryId { get; }
    public string CategoryName { get; }
    public decimal SellingPrice { get; }
    public int PhysicalQuantity { get; }
    public int ReservedQuantity { get; }
    public int AvailableQuantity => PhysicalQuantity - ReservedQuantity;
    public int MinimumStockAlert { get; }
    public bool? IsSerialTracked { get; }
    public InventoryStockStatus Status => AvailableQuantity == 0
        ? InventoryStockStatus.OutOfStock
        : AvailableQuantity <= MinimumStockAlert
            ? InventoryStockStatus.LowStock
            : InventoryStockStatus.Available;

    public string Sku => string.IsNullOrWhiteSpace(VariantSku) ? ProductSku : VariantSku;
    public string StatusLabel => Status switch
    {
        InventoryStockStatus.Available => "Còn hàng",
        InventoryStockStatus.LowStock => "Sắp hết",
        InventoryStockStatus.OutOfStock => "Hết hàng",
        _ => "—"
    };

    public StockBalance(
        string productId,
        string productName,
        string productSku,
        string? variantId,
        string? variantName,
        string? variantSku,
        string categoryId,
        string categoryName,
        decimal sellingPrice,
        int physicalQuantity,
        int reservedQuantity,
        int minimumStockAlert,
        bool? isSerialTracked = false)
    {
        if (string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("Product ID is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required.", nameof(productName));
        if (string.IsNullOrWhiteSpace(productSku)) throw new ArgumentException("Product SKU is required.", nameof(productSku));
        if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category ID is required.", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(categoryName)) throw new ArgumentException("Category name is required.", nameof(categoryName));
        if (sellingPrice < 0) throw new ArgumentOutOfRangeException(nameof(sellingPrice));
        if (physicalQuantity < 0) throw new ArgumentOutOfRangeException(nameof(physicalQuantity));
        if (reservedQuantity < 0 || reservedQuantity > physicalQuantity)
            throw new ArgumentOutOfRangeException(nameof(reservedQuantity), "Reserved quantity must be between zero and physical quantity.");
        if (minimumStockAlert < 0) throw new ArgumentOutOfRangeException(nameof(minimumStockAlert));

        ProductId = productId;
        ProductName = productName;
        ProductSku = productSku;
        VariantId = variantId;
        VariantName = variantName;
        VariantSku = variantSku;
        CategoryId = categoryId;
        CategoryName = categoryName;
        SellingPrice = sellingPrice;
        PhysicalQuantity = physicalQuantity;
        ReservedQuantity = reservedQuantity;
        MinimumStockAlert = minimumStockAlert;
        IsSerialTracked = isSerialTracked;
    }
}

public sealed record StockListQuery(
    string? SearchText = null,
    string? CategoryId = null,
    InventoryStockStatus Status = InventoryStockStatus.All,
    int PageNumber = 1,
    int PageSize = 8);

public sealed record StockListPage(
    IReadOnlyList<StockBalance> Items,
    int TotalItems,
    int PageNumber,
    int PageSize,
    bool IsPreviewData)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public int FirstItemNumber => TotalItems == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int LastItemNumber => TotalItems == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalItems);
}
