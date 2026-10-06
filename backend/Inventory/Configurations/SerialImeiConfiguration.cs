using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Configurations;

public class SerialImeiConfiguration : IEntityTypeConfiguration<SerialImei>
{
    public void Configure(EntityTypeBuilder<SerialImei> builder)
    {
        builder.ToTable("serial_imeis", t =>
        {
            t.HasCheckConstraint("ck_serial_imeis_identifier_present", "NULLIF(BTRIM(serial_number), '') IS NOT NULL OR NULLIF(BTRIM(imei), '') IS NOT NULL");
            t.HasCheckConstraint("ck_serial_imeis_imei_format", "imei IS NULL OR imei ~ '^[0-9]{15}$'");
            t.HasCheckConstraint("ck_serial_imeis_unit_cost", "unit_cost IS NULL OR unit_cost >= 0");
            t.HasCheckConstraint("ck_serial_imeis_warranty_months", "warranty_months >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number");
        builder.Property(x => x.Imei).HasColumnName("imei");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(SerialImeiStatus.InStock).IsRequired();
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2);
        builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder.Property(x => x.WarrantyMonths).HasColumnName("warranty_months").HasDefaultValue(12).IsRequired();
        builder.Property(x => x.WarrantyStartAt).HasColumnName("warranty_start_at").HasColumnType("timestamptz");
        builder.Property(x => x.WarrantyEndAt).HasColumnName("warranty_end_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_serial_imeis_product_id");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("idx_serial_imeis_variant_id");
        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("idx_serial_imeis_po_id");
        builder.HasIndex(x => x.SerialNumber).IsUnique().HasDatabaseName("idx_serial_imeis_serial");
        builder.HasIndex(x => x.Imei).IsUnique().HasDatabaseName("idx_serial_imeis_imei");
        builder.HasIndex(x => new { x.Id, x.ProductId }).IsUnique().HasDatabaseName("uq_serial_imeis_id_product");
        builder.HasIndex(x => new { x.ProductId, x.Status })
            .HasFilter("status = 'InStock'")
            .HasDatabaseName("idx_serial_imeis_instock");

        // Relationships
        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => new { x.VariantId, x.ProductId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
