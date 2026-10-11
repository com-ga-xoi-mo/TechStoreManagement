namespace TechStore.Shared.DTOs;

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? SerialImeiId { get; set; }
    public string? Imei { get; set; }
    public string? SerialNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
}
