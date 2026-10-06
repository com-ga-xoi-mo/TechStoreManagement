using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Configurations;

public class PosSessionConfiguration : IEntityTypeConfiguration<PosSession>
{
    public void Configure(EntityTypeBuilder<PosSession> builder)
    {
        builder.ToTable("pos_sessions", t =>
        {
            t.HasCheckConstraint("ck_pos_sessions_opening_ge_zero", "opening_balance >= 0");
            t.HasCheckConstraint("ck_pos_sessions_cash_sales_ge_zero", "cash_sales_total >= 0");
            t.HasCheckConstraint("ck_pos_sessions_vietqr_sales_ge_zero", "vietqr_sales_total >= 0");
            t.HasCheckConstraint("ck_pos_sessions_card_sales_ge_zero", "card_sales_total >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CashierUserId).HasColumnName("cashier_user_id").IsRequired();
        builder.Property(x => x.OpeningBalance).HasColumnName("opening_balance").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.ClosingBalance).HasColumnName("closing_balance").HasPrecision(18, 2);
        builder.Property(x => x.CashSalesTotal).HasColumnName("cash_sales_total").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.VietQrSalesTotal).HasColumnName("vietqr_sales_total").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.CardSalesTotal).HasColumnName("card_sales_total").HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(PosSessionStatus.Open).IsRequired();
        builder.Property(x => x.OpenedAt).HasColumnName("opened_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.CashierUserId).HasDatabaseName("idx_pos_sessions_cashier_id");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_pos_sessions_status");
        builder.HasIndex(x => x.OpenedAt).HasDatabaseName("idx_pos_sessions_opened_at");
        builder.HasIndex(x => x.CashierUserId)
            .IsUnique()
            .HasFilter("status = 'Open'")
            .HasDatabaseName("uq_pos_sessions_cashier_open");

        // Relationships
        builder.HasOne(x => x.CashierUser)
            .WithMany()
            .HasForeignKey(x => x.CashierUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
