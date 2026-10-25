using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spec_definitions",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    value_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spec_definitions", x => x.key);
                    table.CheckConstraint("ck_spec_definitions_key_snake_case", "key ~ '^[a-z][a-z0-9_]*$'");
                    table.CheckConstraint("ck_spec_definitions_value_kind", "value_kind IN ('number', 'string', 'boolean')");
                });

            migrationBuilder.InsertData(
                table: "spec_definitions",
                columns: new[] { "key", "created_at", "label", "unit", "value_kind" },
                values: new object[,]
                {
                    { "battery_hours", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Thời lượng pin", "giờ", "number" },
                    { "battery_mah", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Dung lượng pin", "mAh", "number" },
                    { "bluetooth", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Bluetooth", null, "string" },
                    { "color", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Màu sắc", null, "string" },
                    { "cpu", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Vi xử lý (CPU)", null, "string" },
                    { "dpi", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Độ nhạy chuột (DPI)", null, "number" },
                    { "interface", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Cổng kết nối", null, "string" },
                    { "layout", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Bố cục phím", null, "string" },
                    { "length_m", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Chiều dài cáp", "m", "number" },
                    { "microphone", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Micro", null, "boolean" },
                    { "noise_cancelling", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Chống ồn", null, "boolean" },
                    { "ports", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Số cổng", null, "number" },
                    { "power_w", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Công suất", "W", "number" },
                    { "ram_gb", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "RAM", "GB", "number" },
                    { "resolution", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Độ phân giải", null, "string" },
                    { "size_in", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Kích thước màn hình", "inch", "number" },
                    { "storage_gb", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Bộ nhớ trong", "GB", "number" },
                    { "weight_g", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Trọng lượng", "g", "number" },
                    { "wireless", new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Không dây", null, "boolean" }
                });

            // Label uniqueness ignoring letter case (design D13); EF cannot model an expression index.
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_spec_definitions_label_lower ON spec_definitions (lower(label));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_spec_definitions_label_lower;");

            migrationBuilder.DropTable(
                name: "spec_definitions");
        }
    }
}
