namespace TechStore.Client.Models.Orders;

/// <summary>A device that can still be sold. Only identifiers are kept; cost and other inventory data are dropped.</summary>
public sealed record AvailableDevice(Guid Id, string? Imei, string? SerialNumber)
{
    public string Display => !string.IsNullOrWhiteSpace(Imei) ? Imei!
        : !string.IsNullOrWhiteSpace(SerialNumber) ? SerialNumber!
        : Id.ToString("D");
}

/// <summary>InStock devices of one product/variant. <see cref="TotalCount"/> can exceed <see cref="Items"/> when the API page is capped.</summary>
public sealed record AvailableDevicePage(IReadOnlyList<AvailableDevice> Items, int TotalCount)
{
    public bool IsTruncated => TotalCount > Items.Count;
}

/// <summary>One line of the order being built. Amounts here are estimates: the server prices the real order.</summary>
public sealed record OrderDraftLine(
    Guid ProductId,
    string ProductName,
    string Sku,
    Guid? VariantId,
    string? VariantName,
    bool IsSerialTracked,
    Guid? SerialImeiId,
    string? Imei,
    string? SerialNumber,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount)
{
    public decimal GrossAmount => UnitPrice * Quantity;

    public decimal EstimatedTotal => GrossAmount - DiscountAmount;
}
