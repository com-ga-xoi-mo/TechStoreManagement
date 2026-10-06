using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Customers.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", t =>
        {
            t.HasCheckConstraint("ck_customers_loyalty_points_ge_zero", "loyalty_points >= 0");
            t.HasCheckConstraint("ck_customers_dob_valid", "date_of_birth <= CURRENT_DATE");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.FullName).HasColumnName("full_name").IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").IsRequired();
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.LoyaltyPoints).HasColumnName("loyalty_points").HasDefaultValue(0);
        builder.Property(x => x.Tier).HasColumnName("tier").HasConversion<string>().HasDefaultValue(CustomerTier.Standard).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.Phone).IsUnique().HasDatabaseName("idx_customers_phone");
        builder.HasIndex(x => x.Tier).HasDatabaseName("idx_customers_tier");
    }
}
