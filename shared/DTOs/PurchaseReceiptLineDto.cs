namespace TechStore.Shared.DTOs;

public class PurchaseReceiptLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? VariantSku { get; set; }
    public int OrderedQuantity { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsSerialTracked { get; set; }
    public int WarrantyMonths { get; set; }
    public IReadOnlyList<PurchaseReceiptDeviceDto> Devices { get; set; } = [];
}
