using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Inventory.Configurations;

public sealed class WarrantyCaseConfiguration : IEntityTypeConfiguration<WarrantyCase>
{
    public void Configure(EntityTypeBuilder<WarrantyCase> b)
    {
        b.ToTable("warranty_cases", t =>
        {
            t.HasCheckConstraint("ck_warranty_cases_dates", "warranty_start_at <= opened_at AND opened_at < warranty_end_at AND (closed_at IS NULL OR closed_at >= opened_at)");
            t.HasCheckConstraint("ck_warranty_cases_outcome", """
                (status = 'UnderRepair' AND closed_by_id IS NULL AND closed_at IS NULL AND resolution_operation_id IS NULL
                    AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)
                OR (status IN ('ReturnedToCustomer','Replaced') AND closed_by_id IS NOT NULL AND closed_at IS NOT NULL AND resolution_operation_id IS NOT NULL
                    AND ((status = 'ReturnedToCustomer' AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)
                      OR (status = 'Replaced' AND replacement_device_id IS NOT NULL AND replacement_device_id <> device_id AND movement_id IS NOT NULL)))
                """);
            t.HasCheckConstraint("ck_warranty_cases_cost", "replacement_unit_cost IS NULL OR replacement_unit_cost >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.OrderItemId).HasColumnName("order_item_id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.DeviceId).HasColumnName("device_id");
        b.Property(x => x.OpenedById).HasColumnName("opened_by_id");
        b.Property(x => x.OpenedAt).HasColumnName("opened_at").HasColumnType("timestamptz");
        b.Property(x => x.WarrantyStartAt).HasColumnName("warranty_start_at").HasColumnType("timestamptz");
        b.Property(x => x.WarrantyEndAt).HasColumnName("warranty_end_at").HasColumnType("timestamptz");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        b.Property(x => x.ReceiveOperationId).HasColumnName("receive_operation_id");
        b.Property(x => x.ResolutionOperationId).HasColumnName("resolution_operation_id");
        b.Property(x => x.ClosedById).HasColumnName("closed_by_id");
        b.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamptz");
        b.Property(x => x.ReplacementDeviceId).HasColumnName("replacement_device_id");
        b.Property(x => x.MovementId).HasColumnName("movement_id");
        b.Property(x => x.ReplacementUnitCost).HasColumnName("replacement_unit_cost").HasPrecision(18, 2);

        b.HasOne<OrderItem>().WithMany().HasForeignKey(x => new { x.OrderItemId, x.OrderId })
            .HasPrincipalKey(x => new { x.Id, x.OrderId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SerialImei>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SerialImei>().WithMany().HasForeignKey(x => x.ReplacementDeviceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OpenedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ClosedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InventoryOperation>().WithMany().HasForeignKey(x => x.ReceiveOperationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InventoryOperation>().WithMany().HasForeignKey(x => x.ResolutionOperationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InventoryMovement>().WithMany().HasForeignKey(x => x.MovementId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OrderId).HasDatabaseName("idx_warranty_cases_order");
        b.HasIndex(x => new { x.OrderItemId, x.OrderId }).HasDatabaseName("idx_warranty_cases_item_order");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("idx_warranty_cases_customer");
        b.HasIndex(x => x.DeviceId).HasDatabaseName("idx_warranty_cases_device");
        b.HasIndex(x => x.ReplacementDeviceId).HasDatabaseName("idx_warranty_cases_replacement");
        b.HasIndex(x => x.OpenedById).HasDatabaseName("idx_warranty_cases_opened_by");
        b.HasIndex(x => x.ClosedById).HasDatabaseName("idx_warranty_cases_closed_by");
        b.HasIndex(x => x.ReceiveOperationId).IsUnique().HasDatabaseName("uq_warranty_cases_receive_op");
        b.HasIndex(x => x.ResolutionOperationId).IsUnique().HasDatabaseName("uq_warranty_cases_resolution_op");
        b.HasIndex(x => x.MovementId).IsUnique().HasDatabaseName("uq_warranty_cases_movement");
    }
}
