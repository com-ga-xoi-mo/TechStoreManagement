using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Warranty.Entities;

namespace TechStore.Api.Warranty.Configurations;

public sealed class WarrantyCaseEventConfiguration : IEntityTypeConfiguration<WarrantyCaseEvent>
{
    public void Configure(EntityTypeBuilder<WarrantyCaseEvent> builder)
    {
        const string statuses = "('Received','Repairing','PendingReplacement','Repaired','Rejected','ReturnedToCustomer','Replaced')";
        builder.ToTable("warranty_case_events", table =>
        {
            table.HasCheckConstraint("ck_warranty_case_events_from_status", $"from_status IN {statuses}");
            table.HasCheckConstraint("ck_warranty_case_events_to_status", $"to_status IN {statuses}");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CaseId).HasColumnName("case_id");
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasConversion<string>();
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasConversion<string>();
        builder.Property(x => x.ActorId).HasColumnName("actor_id");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.HasOne<WarrantyCase>().WithMany(x => x.Events).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CaseId, x.CreatedAt }).HasDatabaseName("idx_warranty_case_events_case_id");
        builder.HasIndex(x => x.ActorId).HasDatabaseName("idx_warranty_case_events_actor_id");
    }
}
