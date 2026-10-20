namespace TechStore.Client.Models.Customers;

public sealed record CustomerLoyaltyTransaction(
    Guid Id,
    int PointsChange,
    int BalanceAfter,
    string Reason,
    Guid? OrderId,
    DateTime CreatedAt);

public sealed record CustomerOrder(
    Guid Id,
    string OrderCode,
    string Status,
    string PaymentMethod,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CompletedAt,
    string? PrimaryProductName = null,
    string? PrimaryVariantName = null,
    string? PrimaryCategoryName = null)
{
    public string DisplayProductName => string.IsNullOrWhiteSpace(PrimaryProductName)
        ? $"{ItemCount:N0} sản phẩm"
        : string.IsNullOrWhiteSpace(PrimaryVariantName) ? PrimaryProductName : $"{PrimaryProductName} {PrimaryVariantName}";

    public string StatusTone => Status switch
    {
        "Completed" => "Success",
        "Cancelled" or "Refunded" => "Danger",
        "Processing" or "PartiallyRefunded" => "Warning",
        _ => "Neutral"
    };
}

public sealed record CustomerPurchasedItem(
    Guid Id,
    Guid OrderId,
    string OrderCode,
    DateTime? CompletedAt,
    Guid ProductId,
    Guid? VariantId,
    string ProductName,
    string? VariantName,
    string Sku,
    Guid? SerialImeiId,
    string? SerialNumber,
    string? Imei,
    int Quantity,
    decimal TotalPrice,
    DateTime? WarrantyStartAt = null,
    DateTime? WarrantyEndAt = null,
    string? DeviceStatus = null,
    string? CategoryName = null)
{
    public string ProductIconGlyph => CategoryName switch
    {
        "Smartphones" => "\uE8EA",
        "Laptops" => "\uE7F8",
        _ => "\uE7F6"
    };

    public string ImeiLabel => SerialImeiId is null
        ? "Không quản lý serial/IMEI"
        : $"IMEI: {Imei ?? "—"}";

    public string SerialLabel => SerialImeiId is null
        ? string.Empty
        : $"Serial: {SerialNumber ?? "—"}";

    public string WarrantyLabel
    {
        get
        {
            if (SerialImeiId is null) return "Không quản lý bảo hành theo máy";
            if (WarrantyEndAt is null) return "Chưa có dữ liệu";
            var date = WarrantyEndAt.Value.ToLocalTime();
            return date.Date >= DateTime.Today ? $"Còn hạn đến {date:dd/MM/yyyy}" : $"Hết hạn {date:dd/MM/yyyy}";
        }
    }

    public string WarrantyStartLabel => SerialImeiId is null || WarrantyStartAt is null
        ? string.Empty
        : $"Từ {WarrantyStartAt.Value.ToLocalTime():dd/MM/yyyy}";

    public string WarrantyTone => SerialImeiId is null ? "Danger"
        : WarrantyEndAt is null ? "Neutral"
        : WarrantyEndAt.Value.ToLocalTime().Date >= DateTime.Today ? "Success" : "Danger";

    public string DeviceStatusLabel => SerialImeiId is null
        ? "Không quản lý"
        : DeviceStatus switch
        {
            "Sold" => "Đang sử dụng",
            "Returned" => "Đã trả",
            "UnderRepair" => "Đang sửa chữa",
            "Defective" => "Lỗi",
            "Reserved" => "Đã giữ hàng",
            "InStock" => "Trong kho",
            _ => "Chưa có dữ liệu"
        };

    public string DeviceStatusTone => SerialImeiId is null || DeviceStatus == "Defective"
        ? "Danger"
        : DeviceStatus == "Sold" ? "Success" : "Neutral";
}
