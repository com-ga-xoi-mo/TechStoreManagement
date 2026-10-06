using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("idx_categories_slug");
        builder.HasIndex(x => x.ParentId).HasDatabaseName("idx_categories_parent_id");

        // Relationships
        builder.HasOne(x => x.Parent)
            .WithMany(x => x.SubCategories)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
