using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_subtotal_ge_zero", "subtotal >= 0");
            t.HasCheckConstraint("ck_orders_discount_amount_ge_zero", "discount_amount >= 0");
            t.HasCheckConstraint("ck_orders_tax_amount_ge_zero", "tax_amount >= 0");
            t.HasCheckConstraint("ck_orders_total_amount_ge_zero", "total_amount >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderCode).HasColumnName("order_code").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.CashierUserId).HasColumnName("cashier_user_id").IsRequired();
        builder.Property(x => x.PosSessionId).HasColumnName("pos_session_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(OrderStatus.Pending).IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasDefaultValue(PaymentMethod.Cash).IsRequired();
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.VoucherId).HasColumnName("voucher_id");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.PaidAt).HasColumnName("paid_at").HasColumnType("timestamptz");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_orders_customer_id");
        builder.HasIndex(x => x.CashierUserId).HasDatabaseName("idx_orders_cashier_id");
        builder.HasIndex(x => x.PosSessionId).HasDatabaseName("idx_orders_session_id");
        builder.HasIndex(x => x.VoucherId).HasDatabaseName("idx_orders_voucher_id");
        builder.HasIndex(x => x.OrderCode).IsUnique().HasDatabaseName("idx_orders_code");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_orders_status");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("idx_orders_created_at");

        // Relationships
        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CashierUser)
            .WithMany()
            .HasForeignKey(x => x.CashierUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Voucher)
            .WithMany()
            .HasForeignKey(x => x.VoucherId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
