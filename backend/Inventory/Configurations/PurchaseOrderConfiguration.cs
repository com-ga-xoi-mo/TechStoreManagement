using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", t =>
        {
            t.HasCheckConstraint("ck_purchase_orders_total_cost_ge_zero", "total_cost >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.ReceivedByUserId).HasColumnName("received_by_user_id");
        builder.Property(x => x.PoNumber).HasColumnName("po_number").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(PurchaseOrderStatus.Draft).IsRequired();
        builder.Property(x => x.TotalCost).HasColumnName("total_cost").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.OrderedAt).HasColumnName("ordered_at").HasColumnType("timestamptz");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.SupplierId).HasDatabaseName("idx_purchase_orders_supplier_id");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_purchase_orders_created_by");
        builder.HasIndex(x => x.ReceivedByUserId).HasDatabaseName("idx_purchase_orders_received_by");
        builder.HasIndex(x => x.PoNumber).IsUnique().HasDatabaseName("idx_purchase_orders_po_number");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_purchase_orders_status");

        // Relationships
        builder.HasOne(x => x.Supplier)
            .WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReceivedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
