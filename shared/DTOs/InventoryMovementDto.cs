using TechStore.Shared.Enums;

namespace TechStore.Shared.DTOs;

public sealed class InventoryMovementDto
{
    public Guid Id { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public int QuantityChange { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PerformedByUserName { get; set; } = string.Empty;
}
