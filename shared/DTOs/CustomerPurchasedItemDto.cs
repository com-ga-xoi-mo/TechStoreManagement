namespace TechStore.Shared.DTOs;

public class CustomerPurchasedItemDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? VariantName { get; set; }
    public string Sku { get; set; } = string.Empty;
    public Guid? SerialImeiId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Imei { get; set; }
    public DateTime? WarrantyStartAt { get; set; }
    public DateTime? WarrantyEndAt { get; set; }
    public string? DeviceStatus { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}
