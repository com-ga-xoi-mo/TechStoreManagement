using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements", t =>
        {
            t.HasCheckConstraint("ck_inventory_movements_qty_change_not_zero", "quantity_change <> 0");
            t.HasCheckConstraint("ck_inventory_movements_qty_after_ge_zero", "quantity_after >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.InventoryStockId).HasColumnName("inventory_stock_id").IsRequired();
        builder.Property(x => x.MovementType).HasColumnName("movement_type").HasConversion<string>().IsRequired();
        builder.Property(x => x.QuantityChange).HasColumnName("quantity_change").IsRequired();
        builder.Property(x => x.QuantityAfter).HasColumnName("quantity_after").IsRequired();
        builder.Property(x => x.PerformedByUserId).HasColumnName("performed_by_user_id").IsRequired();
        builder.Property(x => x.PurchaseOrderItemId).HasColumnName("purchase_order_item_id");
        builder.Property(x => x.OrderItemId).HasColumnName("order_item_id");
        builder.Property(x => x.OrderReturnId).HasColumnName("order_return_id");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => new { x.InventoryStockId, x.CreatedAt }).HasDatabaseName("idx_inventory_movements_stock_created_at");
        builder.HasIndex(x => x.PerformedByUserId).HasDatabaseName("idx_inventory_movements_user_id");
        builder.HasIndex(x => x.PurchaseOrderItemId).HasDatabaseName("idx_inventory_movements_po_item_id");
        builder.HasIndex(x => x.OrderItemId).HasDatabaseName("idx_inventory_movements_order_item_id");
        builder.HasIndex(x => x.OrderReturnId).HasDatabaseName("idx_inventory_movements_return_id");

        // Relationships
        builder.HasOne(x => x.InventoryStock)
            .WithMany(x => x.Movements)
            .HasForeignKey(x => x.InventoryStockId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PerformedByUser)
            .WithMany()
            .HasForeignKey(x => x.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
