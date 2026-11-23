using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Configurations;

public class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ToTable("voucher_redemptions", t =>
        {
            t.HasCheckConstraint("ck_voucher_redemptions_status_valid", "status IN ('Reserved', 'Redeemed', 'Released')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.VoucherId).HasColumnName("voucher_id").IsRequired();
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(VoucherRedemptionStatus.Reserved).IsRequired();
        builder.Property(x => x.RedeemedAt).HasColumnName("redeemed_at").HasColumnType("timestamptz");
        builder.Property(x => x.ReleasedAt).HasColumnName("released_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // One use row per order; the unique index also serves the order FK.
        builder.HasIndex(x => x.OrderId).IsUnique().HasDatabaseName("idx_voucher_redemptions_order_id");
        builder.HasIndex(x => x.VoucherId).HasDatabaseName("idx_voucher_redemptions_voucher_id");

        builder.HasOne(x => x.Voucher)
            .WithMany()
            .HasForeignKey(x => x.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
