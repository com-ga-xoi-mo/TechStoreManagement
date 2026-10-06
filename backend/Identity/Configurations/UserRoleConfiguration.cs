using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Identity.Entities;

namespace TechStore.Api.Identity.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(x => new { x.UserId, x.RoleId }).HasName("pk_user_roles");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.Property(x => x.AssignedAt).HasColumnName("assigned_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.RoleId).HasDatabaseName("idx_user_roles_role_id");

        // Relationships
        builder.HasOne(x => x.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
