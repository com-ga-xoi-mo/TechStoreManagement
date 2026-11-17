using TechStore.Shared.Enums;

namespace TechStore.Shared.DTOs;

public class PurchaseReceiptDeviceDto
{
    public Guid Id { get; set; }
    public string? SerialNumber { get; set; }
    public string? Imei { get; set; }
    public SerialImeiStatus Status { get; set; }
}
