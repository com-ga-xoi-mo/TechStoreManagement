using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Orders.Configurations;

public class OrderReturnConfiguration : IEntityTypeConfiguration<OrderReturn>
{
    public void Configure(EntityTypeBuilder<OrderReturn> builder)
    {
        builder.ToTable("order_returns", t =>
        {
            t.HasCheckConstraint("ck_order_returns_qty_gt_zero", "quantity > 0");
            t.HasCheckConstraint("ck_order_returns_refund_amt_ge_zero", "refund_amount >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.OrderItemId).HasColumnName("order_item_id").IsRequired();
        builder.Property(x => x.ProcessedByUserId).HasColumnName("processed_by_user_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").IsRequired();
        builder.Property(x => x.RefundAmount).HasColumnName("refund_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RefundMethod).HasColumnName("refund_method").HasConversion<string>().IsRequired();
        builder.Property(x => x.RefundPosSessionId).HasColumnName("refund_pos_session_id");
        builder.Property(x => x.RefundedAt).HasColumnName("refunded_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsRestocked).HasColumnName("is_restocked").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_returns_order_id");
        builder.HasIndex(x => x.OrderItemId).HasDatabaseName("idx_order_returns_item_id");
        builder.HasIndex(x => x.ProcessedByUserId).HasDatabaseName("idx_order_returns_user_id");
        builder.HasIndex(x => x.RefundPosSessionId).HasDatabaseName("idx_order_returns_refund_session_id");
        builder.HasIndex(x => x.RefundedAt).HasDatabaseName("idx_order_returns_refunded_at");

        // Relationships
        builder.HasOne(x => x.Order)
            .WithMany(x => x.Returns)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OrderItem)
            .WithMany()
            .HasForeignKey(x => new { x.OrderItemId, x.OrderId })
            .HasPrincipalKey(x => new { x.Id, x.OrderId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProcessedByUser)
            .WithMany()
            .HasForeignKey(x => x.ProcessedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
