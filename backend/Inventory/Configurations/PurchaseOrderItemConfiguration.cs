using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Configurations;

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("purchase_order_items", t =>
        {
            t.HasCheckConstraint("ck_po_items_ordered_qty_gt_zero", "ordered_quantity > 0");
            t.HasCheckConstraint("ck_po_items_received_qty_valid", "received_quantity >= 0 AND received_quantity <= ordered_quantity");
            t.HasCheckConstraint("ck_po_items_unit_cost_ge_zero", "unit_cost >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.OrderedQuantity).HasColumnName("ordered_quantity").IsRequired();
        builder.Property(x => x.ReceivedQuantity).HasColumnName("received_quantity").HasDefaultValue(0);
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("idx_po_items_po_id");
        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_po_items_product_id");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("idx_po_items_variant_id");
        builder.HasIndex(x => new { x.PurchaseOrderId, x.ProductId, x.VariantId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("uq_po_items_order_product_variant");

        // Relationships
        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => new { x.VariantId, x.ProductId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
