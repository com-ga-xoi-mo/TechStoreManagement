using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Inventory;

// A device as returned after a status change (defect report or restore).
public sealed record SerialDeviceItem(
    Guid Id,
    string ProductName,
    string? VariantName,
    string? SerialNumber,
    string? Imei,
    SerialImeiStatus Status)
{
    // IMEI when the device has one, otherwise its serial number.
    public string Identifier => !string.IsNullOrWhiteSpace(Imei) ? Imei : SerialNumber ?? string.Empty;

    public static SerialDeviceItem FromDto(SerialImeiDto dto) => new(
        dto.Id,
        dto.ProductName,
        dto.VariantName,
        dto.SerialNumber,
        dto.Imei,
        dto.Status);
}
