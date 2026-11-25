using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Sales.Entities;

namespace TechStore.Api.Sales.Configurations;

public class VietQrBankEventConfiguration : IEntityTypeConfiguration<VietQrBankEvent>
{
    public void Configure(EntityTypeBuilder<VietQrBankEvent> builder)
    {
        builder.ToTable("vietqr_bank_events", t =>
        {
            t.HasCheckConstraint("ck_vietqr_events_amount_gt_zero", "amount > 0");
            t.HasCheckConstraint("ck_vietqr_events_resolution",
                "(status = 'Resolved' AND resolved_at IS NOT NULL AND resolved_by_user_id IS NOT NULL AND resolution_note IS NOT NULL) OR (status <> 'Resolved' AND resolved_at IS NULL AND resolved_by_user_id IS NULL AND resolution_note IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TransferContent).HasColumnName("transfer_content").HasMaxLength(200).IsRequired();
        builder.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(40).IsRequired();
        builder.Property(x => x.TransferredAt).HasColumnName("transferred_at").HasColumnType("timestamptz");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamptz");
        builder.Property(x => x.VietQrTransactionId).HasColumnName("vietqr_transaction_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");
        builder.Property(x => x.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.Provider, x.ProviderTransactionId }).IsUnique().HasDatabaseName("uq_vietqr_events_provider_transaction");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_vietqr_events_status");
        builder.HasIndex(x => x.VietQrTransactionId).HasDatabaseName("idx_vietqr_events_transaction_id");
        builder.HasIndex(x => x.ResolvedByUserId).HasDatabaseName("idx_vietqr_events_resolved_by");
        builder.HasIndex(x => x.ReceivedAt).HasDatabaseName("idx_vietqr_events_received_at");

        builder.HasOne(x => x.VietQrTransaction).WithMany().HasForeignKey(x => x.VietQrTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedByUser).WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
