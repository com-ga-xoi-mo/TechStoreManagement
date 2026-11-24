namespace TechStore.Api.Warranty.Entities;

public sealed class WarrantyCaseEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public WarrantyCaseStatus? FromStatus { get; set; }
    public WarrantyCaseStatus ToStatus { get; set; }
    public Guid ActorId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
