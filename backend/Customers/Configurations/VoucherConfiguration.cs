using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Customers.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Configurations;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers", t =>
        {
            t.HasCheckConstraint("ck_vouchers_discount_value_valid", "discount_value > 0 AND (discount_type <> 'Percentage' OR discount_value <= 100)");
            t.HasCheckConstraint("ck_vouchers_min_order_ge_zero", "min_order_amount >= 0");
            t.HasCheckConstraint("ck_vouchers_max_discount_ge_zero", "max_discount_amount IS NULL OR max_discount_amount >= 0");
            t.HasCheckConstraint("ck_vouchers_usage_limit_gt_zero", "usage_limit > 0");
            t.HasCheckConstraint("ck_vouchers_used_count_valid", "used_count >= 0 AND used_count <= usage_limit");
            t.HasCheckConstraint("ck_vouchers_expires_after_created", "expires_at > created_at");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Code).HasColumnName("code").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.DiscountType).HasColumnName("discount_type").HasConversion<string>().HasDefaultValue(VoucherType.Percentage).IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnName("discount_value").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinOrderAmount).HasColumnName("min_order_amount").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.MaxDiscountAmount).HasColumnName("max_discount_amount").HasPrecision(18, 2);
        builder.Property(x => x.UsageLimit).HasColumnName("usage_limit").HasDefaultValue(100);
        builder.Property(x => x.UsedCount).HasColumnName("used_count").HasDefaultValue(0);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("idx_vouchers_code");
        builder.HasIndex(x => new { x.IsActive, x.ExpiresAt })
            .HasFilter("is_active = true")
            .HasDatabaseName("idx_vouchers_active");
    }
}
