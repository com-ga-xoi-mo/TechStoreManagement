using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_base_price_ge_zero", "base_price >= 0");
            t.HasCheckConstraint("ck_products_cost_price_ge_zero", "cost_price >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Sku).HasColumnName("sku").IsRequired();
        builder.Property(x => x.Barcode).HasColumnName("barcode");
        builder.Property(x => x.Brand).HasColumnName("brand").IsRequired();
        builder.Property(x => x.BasePrice).HasColumnName("base_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CostPrice).HasColumnName("cost_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.ImageUrl).HasColumnName("image_url");
        builder.Property(x => x.Specs).HasColumnName("specs").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(x => x.IsSerialTracked).HasColumnName("is_serial_tracked").HasDefaultValue(true);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.CategoryId).HasDatabaseName("idx_products_category_id");
        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("idx_products_sku");
        builder.HasIndex(x => x.Barcode).HasDatabaseName("idx_products_barcode");
        builder.HasIndex(x => x.Specs).HasMethod("gin").HasOperators("jsonb_path_ops").HasDatabaseName("idx_products_specs_gin");

        // Relationships
        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
