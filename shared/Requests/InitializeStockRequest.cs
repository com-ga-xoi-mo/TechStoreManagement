namespace TechStore.Shared.Requests;

public class InitializeStockRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int? Quantity { get; set; }
    public int? MinStockAlert { get; set; }
    // Cost of each opening unit. Required for goods without serial/IMEI; ignored for serial-tracked goods,
    // whose cost is recorded per device.
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }
}
