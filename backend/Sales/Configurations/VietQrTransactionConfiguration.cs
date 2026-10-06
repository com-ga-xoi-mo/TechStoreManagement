using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Configurations;

public class VietQrTransactionConfiguration : IEntityTypeConfiguration<VietQrTransaction>
{
    public void Configure(EntityTypeBuilder<VietQrTransaction> builder)
    {
        builder.ToTable("vietqr_transactions", t =>
        {
            t.HasCheckConstraint("ck_vietqr_tx_amount_gt_zero", "amount > 0");
            t.HasCheckConstraint("chk_vietqr_provider_reference_pair", "(provider IS NULL) = (provider_transaction_id IS NULL)");
            t.HasCheckConstraint("chk_vietqr_expiry_after_creation", "expires_at > created_at");
            t.HasCheckConstraint("chk_vietqr_confirmation_evidence", "(status = 'Confirmed' AND confirmed_at IS NOT NULL AND (confirmed_by_user_id IS NOT NULL OR provider_transaction_id IS NOT NULL)) OR (status <> 'Confirmed' AND confirmed_at IS NULL AND confirmed_by_user_id IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.PosSessionId).HasColumnName("pos_session_id");
        builder.Property(x => x.TransactionCode).HasColumnName("transaction_code").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.ProviderTransactionId).HasColumnName("provider_transaction_id");
        builder.Property(x => x.BankBin).HasColumnName("bank_bin").IsRequired();
        builder.Property(x => x.BankAccountNumber).HasColumnName("bank_account_number").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.QrContent).HasColumnName("qr_content").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(VietQrStatus.Pending).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
        builder.Property(x => x.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_vietqr_tx_order_id");
        builder.HasIndex(x => x.PosSessionId).HasDatabaseName("idx_vietqr_tx_session_id");
        builder.HasIndex(x => x.TransactionCode).IsUnique().HasDatabaseName("idx_vietqr_tx_code");
        builder.HasIndex(x => new { x.Provider, x.ProviderTransactionId })
            .IsUnique()
            .HasDatabaseName("uq_vietqr_provider_transaction");
        builder.HasIndex(x => x.ConfirmedByUserId).HasDatabaseName("idx_vietqr_tx_confirmed_by");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_vietqr_tx_status");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_vietqr_tx_expires_at");
        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("status = 'Confirmed'")
            .HasDatabaseName("uq_vietqr_confirmed_order");

        // Relationships
        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PosSession)
            .WithMany(x => x.VietQrTransactions)
            .HasForeignKey(x => x.PosSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(x => x.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
