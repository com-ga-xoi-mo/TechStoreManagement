namespace TechStore.Api.Inventory.Entities;

public enum WarrantyCaseStatus { UnderRepair, ReturnedToCustomer, Replaced }

public sealed class WarrantyCase
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid OpenedById { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime WarrantyStartAt { get; set; }
    public DateTime WarrantyEndAt { get; set; }
    public WarrantyCaseStatus Status { get; set; }
    public Guid ReceiveOperationId { get; set; }
    public Guid? ResolutionOperationId { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ReplacementDeviceId { get; set; }
    public Guid? MovementId { get; set; }
    public decimal? ReplacementUnitCost { get; set; }
}
