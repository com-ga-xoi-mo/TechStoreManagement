using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Configurations;

public class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.ToTable("inventory_stocks", t =>
        {
            t.HasCheckConstraint("ck_inventory_stocks_quantity_ge_zero", "quantity >= 0");
            t.HasCheckConstraint("ck_inventory_stocks_reserved_ge_zero", "reserved_quantity >= 0");
            t.HasCheckConstraint("ck_inventory_stocks_reserved_le_quantity", "reserved_quantity <= quantity");
            t.HasCheckConstraint("ck_inventory_stocks_min_alert_ge_zero", "min_stock_alert >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(0);
        builder.Property(x => x.ReservedQuantity).HasColumnName("reserved_quantity").HasDefaultValue(0);
        builder.Property(x => x.MinStockAlert).HasColumnName("min_stock_alert").HasDefaultValue(5);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.VariantId).HasDatabaseName("idx_inventory_stocks_variant_id");
        builder.HasIndex(x => new { x.ProductId, x.VariantId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("idx_inventory_stocks_product_variant");

        // Relationships
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
