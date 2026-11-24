namespace TechStore.Api.Warranty.Entities;

public enum WarrantyCaseStatus
{
    Received,
    Repairing,
    PendingReplacement,
    Repaired,
    Rejected,
    ReturnedToCustomer,
    Replaced
}

public sealed class WarrantyCase
{
    public Guid Id { get; set; }
    public string CaseCode { get; set; } = null!;
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
    public string? IssueDescription { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? InspectionNote { get; set; }
    public string? RejectionReason { get; set; }
    public ICollection<WarrantyCaseEvent> Events { get; } = new List<WarrantyCaseEvent>();
}
