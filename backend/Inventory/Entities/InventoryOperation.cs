namespace TechStore.Api.Inventory.Entities;

public class InventoryOperation
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string Payload { get; set; } = "";
    public Guid? InspectionReturnId { get; set; }
    public Guid? MovementId { get; set; }
    public decimal? UnitCost { get; set; }
    public DateTime CreatedAt { get; set; }
}
