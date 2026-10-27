namespace TechStore.Shared.DTOs;

public sealed class InventoryStockDetailDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? VariantSku { get; set; }
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal? SellingPrice { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Quantity - ReservedQuantity;
    public int? TotalSerialsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<InventoryMovementDto> RecentMovements { get; set; } = [];
}
