using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Configurations;

public sealed class ReturnCaseConfiguration : IEntityTypeConfiguration<ReturnCase>
{
    public void Configure(EntityTypeBuilder<ReturnCase> b)
    {
        b.ToTable("return_cases", t => t.HasCheckConstraint("ck_return_cases_status",
            "status IN ('Pending','AwaitingBank','Refunded','Failed')"));
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.RequestedById).HasColumnName("requested_by_id");
        b.Property(x => x.WasUndelivered).HasColumnName("was_undelivered").HasDefaultValue(false);
        b.Property(x => x.PayoutActorId).HasColumnName("payout_actor_id");
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
        b.Property(x => x.Method).HasColumnName("method").HasMaxLength(16);
        b.Property(x => x.BankReference).HasColumnName("bank_reference").HasMaxLength(128);
        b.Property(x => x.ReplacementOrderId).HasColumnName("replacement_order_id");
        b.Property(x => x.BankApprovedAt).HasColumnName("bank_approved_at");
        b.Property(x => x.RefundedAt).HasColumnName("refunded_at");
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.ReplacementOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PayoutActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OrderId).HasDatabaseName("idx_return_cases_order_id");
        b.HasIndex(x => x.RequestedById).HasDatabaseName("idx_return_cases_requested_by_id");
        b.HasIndex(x => x.PayoutActorId).HasDatabaseName("idx_return_cases_payout_actor_id");
        b.HasIndex(x => x.ReplacementOrderId).IsUnique().HasDatabaseName("uq_return_cases_replacement");
        b.HasIndex(x => x.BankReference).IsUnique().HasDatabaseName("uq_return_cases_bank_ref");
    }
}

public sealed class ReturnCaseLineConfiguration : IEntityTypeConfiguration<ReturnCaseLine>
{
    public void Configure(EntityTypeBuilder<ReturnCaseLine> b)
    {
        b.ToTable("return_case_lines", t => t.HasCheckConstraint("ck_return_case_lines_amount",
            "quantity > 0 AND net_amount >= 0 AND discount_amount >= 0 AND tax_amount >= 0"));
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.CaseId).HasColumnName("case_id");
        b.Property(x => x.OrderItemId).HasColumnName("order_item_id");
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.NetAmount).HasColumnName("net_amount").HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 2);
        b.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 2);
        b.HasOne<ReturnCase>().WithMany(x => x.Lines).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CaseId, x.OrderItemId }).IsUnique().HasDatabaseName("uq_return_case_lines_case_item");
        b.HasIndex(x => x.OrderItemId).HasDatabaseName("idx_return_case_lines_item");
    }
}
