using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;

namespace TechStore.Api.Sales.Configurations;

public sealed class CashRefundPayoutConfiguration : IEntityTypeConfiguration<CashRefundPayout>
{
    public void Configure(EntityTypeBuilder<CashRefundPayout> b)
    {
        b.ToTable("cash_refund_payouts", t => t.HasCheckConstraint("ck_cash_refund_amount", "amount >= 0"));
        b.HasKey(x => x.RefundId);
        b.Property(x => x.RefundId).HasColumnName("refund_id").ValueGeneratedNever();
        b.Property(x => x.ShiftId).HasColumnName("shift_id");
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        b.HasOne<ReturnCase>().WithMany().HasForeignKey(x => x.RefundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PosSession>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ShiftId).HasDatabaseName("idx_cash_refund_payouts_shift");
    }
}
