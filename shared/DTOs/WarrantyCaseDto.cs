namespace TechStore.Shared.DTOs;

public sealed record WarrantyCaseDto
{
    public Guid Id { get; init; }
    public string CaseCode { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public Guid OrderItemId { get; init; }
    public string? OrderCode { get; init; }
    public DateTime? PurchasedAt { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid DeviceId { get; init; }
    public string? DeviceCode { get; init; }
    public Guid ProductId { get; init; }
    public Guid? VariantId { get; init; }
    public string? ProductName { get; init; }
    public string? VariantName { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime OpenedAt { get; init; }
    public Guid OpenedById { get; init; }
    public DateTime WarrantyStartAt { get; init; }
    public DateTime WarrantyEndAt { get; init; }
    public string? IssueDescription { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? InspectionNote { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime? ClosedAt { get; init; }
    public Guid? ClosedById { get; init; }
    public Guid? ReplacementDeviceId { get; init; }
    public string? ReplacementDeviceCode { get; init; }
    public Guid? MovementId { get; init; }
    public decimal? ReplacementUnitCost { get; init; }
    public IReadOnlyList<WarrantyCaseEventDto> Events { get; init; } = [];
}

public sealed record WarrantyCaseEventDto(
    Guid Id, string? FromStatus, string ToStatus, Guid ActorId, string ActorName, string? Note, DateTime CreatedAt);

public sealed record WarrantyCaseSummaryDto(
    Guid Id, string CaseCode, string Status, DateTime OpenedAt, string ProductName,
    string? DeviceCode, string OrderCode, string? CustomerName, string? ContactPhone, decimal? ReplacementUnitCost);
