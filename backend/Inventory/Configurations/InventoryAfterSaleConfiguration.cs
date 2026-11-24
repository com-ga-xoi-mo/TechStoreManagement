using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Inventory.Configurations;

public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> b)
    {
        b.ToTable("inventory_reservations", t => t.HasCheckConstraint("ck_inventory_reservations_quantity", "quantity > 0"));
        b.HasKey(x => x.OrderItemId);
        b.Property(x => x.OrderItemId).HasColumnName("order_item_id").ValueGeneratedNever();
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.State).HasColumnName("state").HasConversion<string>();
        b.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryOperationConfiguration : IEntityTypeConfiguration<InventoryOperation>
{
    public void Configure(EntityTypeBuilder<InventoryOperation> b)
    {
        b.ToTable("inventory_operations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Kind).HasColumnName("kind");
        b.Property(x => x.Payload).HasColumnName("payload");
        b.Property(x => x.InspectionReturnId).HasColumnName("inspection_return_id");
        b.HasOne<OrderReturn>().WithMany().HasForeignKey(x => x.InspectionReturnId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InspectionReturnId).IsUnique().HasDatabaseName("uq_inventory_operations_inspection_return");
        b.Property(x => x.MovementId).HasColumnName("movement_id");
        b.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        b.HasOne<InventoryMovement>().WithMany().HasForeignKey(x => x.MovementId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MovementId).HasDatabaseName("idx_inventory_operations_movement_id");
    }
}
