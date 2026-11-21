using System.Globalization;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Client.Models.Inventory;

public static class ReceiptStatusLabels
{
    public static string Format(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Ordered => "Chờ nhận",
        PurchaseOrderStatus.PartiallyReceived => "Đã nhận một phần",
        PurchaseOrderStatus.Received => "Đã nhận đủ",
        PurchaseOrderStatus.Cancelled => "Đã hủy phần còn lại",
        _ => "Nháp"
    };
}

public sealed record PurchaseReceiptSummary(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string SupplierName,
    string? SupplierDocumentNumber,
    DateTime? ReceivedAt,
    Guid? ReceivedByUserId,
    string? ReceivedByName,
    int LineCount,
    int TotalQuantity,
    decimal TotalCost,
    PurchaseOrderStatus Status = PurchaseOrderStatus.Received)
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    public string TotalCostFormatted => TotalCost.ToString("N0", ViCulture) + " ₫";
    public string ReceivedAtFormatted => ReceivedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string ReceiverDisplay => string.IsNullOrWhiteSpace(ReceivedByName) ? "—" : ReceivedByName;
    public string DocumentNumberDisplay => string.IsNullOrWhiteSpace(SupplierDocumentNumber) ? "—" : SupplierDocumentNumber;

    public static PurchaseReceiptSummary FromDto(PurchaseReceiptSummaryDto dto) =>
        new(
            dto.Id,
            dto.PoNumber,
            dto.SupplierId,
            dto.SupplierName,
            dto.SupplierDocumentNumber,
            dto.ReceivedAt,
            dto.ReceivedByUserId,
            dto.ReceivedByName,
            dto.LineCount,
            dto.TotalQuantity,
            dto.TotalCost, dto.Status);
}

public sealed record PurchaseReceiptDevice(
    Guid Id,
    string? SerialNumber,
    string? Imei,
    SerialImeiStatus Status)
{
    public string Identifier => Imei ?? SerialNumber ?? "—";

    public string StatusLabel => Status switch
    {
        SerialImeiStatus.InStock => "Trong kho",
        SerialImeiStatus.Reserved => "Đang giữ",
        SerialImeiStatus.Sold => "Đã bán",
        SerialImeiStatus.Returned => "Đã trả lại",
        SerialImeiStatus.UnderRepair => "Đang sửa chữa",
        SerialImeiStatus.Defective => "Lỗi / Hư hỏng",
        _ => Status.ToString()
    };

    public static PurchaseReceiptDevice FromDto(PurchaseReceiptDeviceDto dto) =>
        new(
            dto.Id,
            dto.SerialNumber,
            dto.Imei,
            dto.Status);
}

public sealed record PurchaseReceiptLine(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid? VariantId,
    string? VariantName,
    string? VariantSku,
    int Quantity,
    decimal UnitCost,
    decimal LineTotal,
    bool IsSerialTracked,
    int WarrantyMonths,
    IReadOnlyList<PurchaseReceiptDevice> Devices,
    int OrderedQuantity = 0)
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    public string DisplayName => string.IsNullOrWhiteSpace(VariantName) ? ProductName : $"{ProductName} ({VariantName})";
    public string DisplaySku => VariantSku ?? ProductSku;
    public string UnitCostFormatted => UnitCost.ToString("N0", ViCulture) + " ₫";
    public string LineTotalFormatted => LineTotal.ToString("N0", ViCulture) + " ₫";

    public static PurchaseReceiptLine FromDto(PurchaseReceiptLineDto dto) =>
        new(
            dto.Id,
            dto.ProductId,
            dto.ProductName,
            dto.ProductSku,
            dto.VariantId,
            dto.VariantName,
            dto.VariantSku,
            dto.Quantity,
            dto.UnitCost,
            dto.LineTotal,
            dto.IsSerialTracked,
            dto.WarrantyMonths,
            dto.Devices.Select(PurchaseReceiptDevice.FromDto).ToList(),
            dto.OrderedQuantity > 0 ? dto.OrderedQuantity : dto.Quantity);
}

public sealed record PurchaseReceiptDetail(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string SupplierName,
    string? SupplierDocumentNumber,
    Guid? ClientRequestId,
    string? Note,
    DateTime? ReceivedAt,
    Guid? ReceivedByUserId,
    string? ReceivedByName,
    decimal TotalCost,
    IReadOnlyList<PurchaseReceiptLine> Lines,
    PurchaseOrderStatus Status = PurchaseOrderStatus.Received)
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    public string TotalCostFormatted => TotalCost.ToString("N0", ViCulture) + " ₫";
    public string ReceivedAtFormatted => ReceivedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string ReceiverDisplay => string.IsNullOrWhiteSpace(ReceivedByName) ? "—" : ReceivedByName;
    public string DocumentNumberDisplay => string.IsNullOrWhiteSpace(SupplierDocumentNumber) ? "—" : SupplierDocumentNumber;

    public static PurchaseReceiptDetail FromDto(PurchaseReceiptDto dto) =>
        new(
            dto.Id,
            dto.PoNumber,
            dto.SupplierId,
            dto.SupplierName,
            dto.SupplierDocumentNumber,
            dto.ClientRequestId,
            dto.Note,
            dto.ReceivedAt,
            dto.ReceivedByUserId,
            dto.ReceivedByName,
            dto.TotalCost,
            dto.Lines.Select(PurchaseReceiptLine.FromDto).ToList(), dto.Status);
}
