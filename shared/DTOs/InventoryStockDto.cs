namespace TechStore.Shared.DTOs;

public class InventoryStockDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    /// <summary>Display name of the product category.</summary>
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>Current selling price: variant price when a variant exists, otherwise product base price.</summary>
    public decimal? SellingPrice { get; set; }
    /// <summary>Authoritative related Serial/IMEI count when supplied by the inventory API.</summary>
    public int? TotalSerialsCount { get; set; }
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? VariantSku { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Quantity - ReservedQuantity;
    public int MinStockAlert { get; set; }
    public bool IsLowStock => AvailableQuantity <= MinStockAlert;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
