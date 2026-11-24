namespace TechStore.Api.Inventory.Entities;

// The immutable order item identifies the SKU, device (if present), and owner of this hold.
public class InventoryReservation
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public ReservationState State { get; set; } = ReservationState.Held;
}

public enum ReservationState { Held, Released, Sold }
