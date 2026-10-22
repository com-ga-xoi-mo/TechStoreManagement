using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images", table =>
        {
            table.HasCheckConstraint("ck_product_images_sort_order_ge_zero", "sort_order >= 0");
            table.HasCheckConstraint("ck_product_images_size_bytes_range", "size_bytes > 0 AND size_bytes <= 5242880");
            table.HasCheckConstraint("ck_product_images_content_type", "content_type IN ('image/jpeg','image/png','image/webp')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.ProductId, x.SortOrder }).HasDatabaseName("idx_product_images_product_id");
        builder.HasIndex(x => new { x.VariantId, x.ProductId }).HasDatabaseName("idx_product_images_variant_id");
        builder.HasIndex(x => x.ObjectKey).IsUnique().HasDatabaseName("uq_product_images_object_key");

        builder.HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => new { x.VariantId, x.ProductId })
            .HasPrincipalKey(v => new { v.Id, v.ProductId }).OnDelete(DeleteBehavior.Cascade);
    }
}
