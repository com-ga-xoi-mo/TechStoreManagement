namespace TechStore.Shared.DTOs;

public class InventoryStockDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? VariantSku { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Quantity - ReservedQuantity;
    public int MinStockAlert { get; set; }
    public bool IsLowStock => Quantity <= MinStockAlert;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
