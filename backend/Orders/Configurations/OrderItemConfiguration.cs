using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_unit_price_ge_zero", "unit_price >= 0");
            t.HasCheckConstraint("ck_order_items_unit_cost_valid", "unit_cost IS NULL OR unit_cost >= 0");
            t.HasCheckConstraint("ck_order_items_quantity_gt_zero", "quantity > 0");
            t.HasCheckConstraint("ck_order_items_discount_ge_zero", "discount_amount >= 0");
            t.HasCheckConstraint("ck_order_items_total_price_ge_zero", "total_price >= 0");
            t.HasCheckConstraint("chk_order_items_serial_single_unit", "serial_imei_id IS NULL OR quantity = 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.SerialImeiId).HasColumnName("serial_imei_id");
        builder.Property(x => x.ProductName).HasColumnName("product_name").IsRequired();
        builder.Property(x => x.VariantName).HasColumnName("variant_name");
        builder.Property(x => x.Sku).HasColumnName("sku").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2);
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(1);
        builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.TotalPrice).HasColumnName("total_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_items_order_id");
        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_order_items_product_id");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("idx_order_items_variant_id");
        builder.HasIndex(x => x.SerialImeiId).HasDatabaseName("idx_order_items_serial_imei_id");
        builder.HasIndex(x => new { x.Id, x.OrderId }).IsUnique().HasDatabaseName("uq_order_items_id_order");
        builder.HasIndex(x => new { x.OrderId, x.SerialImeiId }).IsUnique().HasDatabaseName("uq_order_items_order_serial");

        // Relationships
        builder.HasOne(x => x.Order)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId)
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

        builder.HasOne(x => x.SerialImei)
            .WithMany()
            .HasForeignKey(x => new { x.SerialImeiId, x.ProductId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
