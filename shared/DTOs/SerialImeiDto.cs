using TechStore.Shared.Enums;

namespace TechStore.Shared.DTOs;

public class SerialImeiDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? SerialNumber { get; set; }
    public string? Imei { get; set; }
    public SerialImeiStatus Status { get; set; }
    public decimal? UnitCost { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public int WarrantyMonths { get; set; }
    public DateTime? WarrantyStartAt { get; set; }
    public DateTime? WarrantyEndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
