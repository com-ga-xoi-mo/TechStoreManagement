namespace TechStore.Shared.Requests;

public sealed class ReceivePurchaseDeliveryRequest
{
    public string? SupplierDocumentNumber { get; set; }
    public Guid ClientRequestId { get; set; }
    public List<PurchaseDeliveryLine> Lines { get; set; } = [];
}

public sealed class PurchaseDeliveryLine
{
    public Guid LineId { get; set; }
    public int Quantity { get; set; }
    public int ExpectedReceivedQuantity { get; set; }
    public List<PurchaseReceiptDeviceRequest> Devices { get; set; } = [];
}
