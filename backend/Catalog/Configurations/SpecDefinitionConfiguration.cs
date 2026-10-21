using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Configurations;

public class SpecDefinitionConfiguration : IEntityTypeConfiguration<SpecDefinition>
{
    private static readonly DateTime SeededAt = new(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<SpecDefinition> builder)
    {
        builder.ToTable("spec_definitions", t =>
        {
            t.HasCheckConstraint("ck_spec_definitions_key_snake_case", "key ~ '^[a-z][a-z0-9_]*$'");
            t.HasCheckConstraint("ck_spec_definitions_value_kind", "value_kind IN ('number', 'string', 'boolean')");
        });

        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(64);
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20);
        builder.Property(x => x.ValueKind).HasColumnName("value_kind").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");

        // Label uniqueness ignoring case is the index idx_spec_definitions_label_lower on lower(label),
        // created with raw SQL in the AddSpecDefinitions migration (EF cannot model an expression index).

        builder.HasData(
            Seed("storage_gb", "Bộ nhớ trong", "GB", SpecValueKinds.Number),
            Seed("ram_gb", "RAM", "GB", SpecValueKinds.Number),
            Seed("cpu", "Vi xử lý (CPU)", null, SpecValueKinds.String),
            Seed("color", "Màu sắc", null, SpecValueKinds.String),
            Seed("battery_mah", "Dung lượng pin", "mAh", SpecValueKinds.Number),
            Seed("battery_hours", "Thời lượng pin", "giờ", SpecValueKinds.Number),
            Seed("resolution", "Độ phân giải", null, SpecValueKinds.String),
            Seed("size_in", "Kích thước màn hình", "inch", SpecValueKinds.Number),
            Seed("power_w", "Công suất", "W", SpecValueKinds.Number),
            Seed("bluetooth", "Bluetooth", null, SpecValueKinds.String),
            Seed("ports", "Số cổng", null, SpecValueKinds.Number),
            Seed("interface", "Cổng kết nối", null, SpecValueKinds.String),
            Seed("length_m", "Chiều dài cáp", "m", SpecValueKinds.Number),
            Seed("wireless", "Không dây", null, SpecValueKinds.Boolean),
            Seed("noise_cancelling", "Chống ồn", null, SpecValueKinds.Boolean),
            Seed("microphone", "Micro", null, SpecValueKinds.Boolean),
            Seed("dpi", "Độ nhạy chuột (DPI)", null, SpecValueKinds.Number),
            Seed("layout", "Bố cục phím", null, SpecValueKinds.String),
            Seed("weight_g", "Trọng lượng", "g", SpecValueKinds.Number));
    }

    private static SpecDefinition Seed(string key, string label, string? unit, string kind) =>
        new() { Key = key, Label = label, Unit = unit, ValueKind = kind, CreatedAt = SeededAt };
}
