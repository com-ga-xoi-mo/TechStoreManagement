using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Customers.Configurations;

public class CustomerLoyaltyPointConfiguration : IEntityTypeConfiguration<CustomerLoyaltyPoint>
{
    public void Configure(EntityTypeBuilder<CustomerLoyaltyPoint> builder)
    {
        builder.ToTable("customer_loyalty_points", t =>
        {
            t.HasCheckConstraint("ck_cust_loyalty_source",
                "(NOT is_sale_accrual OR (order_id IS NOT NULL AND refund_case_id IS NULL AND points_change > 0)) " +
                "AND (refund_case_id IS NULL OR (order_id IS NOT NULL AND points_change < 0))");
            t.HasCheckConstraint("ck_cust_loyalty_points_change_not_zero", "points_change <> 0");
            t.HasCheckConstraint("ck_cust_loyalty_balance_after_ge_zero", "balance_after >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.IsSaleAccrual).HasColumnName("is_sale_accrual").HasDefaultValue(false);
        builder.Property(x => x.RefundCaseId).HasColumnName("refund_case_id");
        builder.Property(x => x.PointsChange).HasColumnName("points_change").IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnName("balance_after").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_cust_loyalty_customer_id");
        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_cust_loyalty_order_id");

        builder.HasIndex(x => x.OrderId, "SaleCreditPerOrder").IsUnique().HasFilter("is_sale_accrual")
            .HasDatabaseName("uq_cust_loyalty_sale_order");
        builder.HasIndex(x => x.RefundCaseId).IsUnique().HasFilter("refund_case_id IS NOT NULL")
            .HasDatabaseName("uq_cust_loyalty_refund_case");

        // Relationships
        builder.HasOne<ReturnCase>().WithMany().HasForeignKey(x => x.RefundCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer)
            .WithMany(x => x.LoyaltyPointsHistory)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
