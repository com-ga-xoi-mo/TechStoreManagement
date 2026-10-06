using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", t =>
        {
            t.HasCheckConstraint("ck_product_variants_price_ge_zero", "price >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.VariantName).HasColumnName("variant_name").IsRequired();
        builder.Property(x => x.Sku).HasColumnName("sku").IsRequired();
        builder.Property(x => x.Barcode).HasColumnName("barcode");
        builder.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Specs).HasColumnName("specs").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_product_variants_product_id");
        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("idx_product_variants_sku");
        builder.HasIndex(x => x.Barcode).HasDatabaseName("idx_product_variants_barcode");
        builder.HasIndex(x => x.Specs).HasMethod("gin").HasOperators("jsonb_path_ops").HasDatabaseName("idx_product_variants_specs_gin");
        builder.HasIndex(x => new { x.Id, x.ProductId }).IsUnique().HasDatabaseName("uq_product_variants_id_product");

        // Relationships
        builder.HasOne(x => x.Product)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
