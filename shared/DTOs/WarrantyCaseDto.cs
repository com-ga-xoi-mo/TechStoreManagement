namespace TechStore.Shared.DTOs;

public sealed record WarrantyCaseDto(Guid Id, Guid OrderId, Guid OrderItemId, Guid? CustomerId, Guid DeviceId,
    string Status, DateTime OpenedAt, DateTime WarrantyStartAt, DateTime WarrantyEndAt, DateTime? ClosedAt,
    Guid? ReplacementDeviceId, Guid? MovementId);
