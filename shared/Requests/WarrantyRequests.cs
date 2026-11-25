namespace TechStore.Shared.Requests;

public sealed record ReceiveWarrantyRequest(Guid OrderId, Guid OrderItemId, Guid DeviceId, Guid? CustomerId);
public sealed record ReplaceWarrantyDeviceRequest(Guid ReplacementDeviceId);
