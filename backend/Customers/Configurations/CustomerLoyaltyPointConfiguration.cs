using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Customers.Entities;

namespace TechStore.Api.Customers.Configurations;

public class CustomerLoyaltyPointConfiguration : IEntityTypeConfiguration<CustomerLoyaltyPoint>
{
    public void Configure(EntityTypeBuilder<CustomerLoyaltyPoint> builder)
    {
        builder.ToTable("customer_loyalty_points", t =>
        {
            t.HasCheckConstraint("ck_cust_loyalty_points_change_not_zero", "points_change <> 0");
            t.HasCheckConstraint("ck_cust_loyalty_balance_after_ge_zero", "balance_after >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.PointsChange).HasColumnName("points_change").IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnName("balance_after").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_cust_loyalty_customer_id");
        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_cust_loyalty_order_id");

        // Relationships
        builder.HasOne(x => x.Customer)
            .WithMany(x => x.LoyaltyPointsHistory)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
