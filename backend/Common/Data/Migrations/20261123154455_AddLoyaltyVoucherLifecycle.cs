using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyVoucherLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_vouchers_used_count_valid",
                table: "vouchers");

            migrationBuilder.AddColumn<int>(
                name: "reserved_count",
                table: "vouchers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "discount_source",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "reference_key",
                table: "customer_loyalty_points",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "voucher_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Reserved"),
                    redeemed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    released_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_redemptions", x => x.id);
                    table.CheckConstraint("ck_voucher_redemptions_status_valid", "status IN ('Reserved', 'Redeemed', 'Released')");
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_vouchers_used_count_valid",
                table: "vouchers",
                sql: "used_count >= 0 AND reserved_count >= 0 AND used_count + reserved_count <= usage_limit");

            migrationBuilder.CreateIndex(
                name: "idx_cust_loyalty_reference_key",
                table: "customer_loyalty_points",
                column: "reference_key",
                unique: true,
                filter: "reference_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_voucher_redemptions_order_id",
                table: "voucher_redemptions",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_voucher_redemptions_voucher_id",
                table: "voucher_redemptions",
                column: "voucher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_redemptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vouchers_used_count_valid",
                table: "vouchers");

            migrationBuilder.DropIndex(
                name: "idx_cust_loyalty_reference_key",
                table: "customer_loyalty_points");

            migrationBuilder.DropColumn(
                name: "reserved_count",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "discount_source",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "reference_key",
                table: "customer_loyalty_points");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vouchers_used_count_valid",
                table: "vouchers",
                sql: "used_count >= 0 AND used_count <= usage_limit");
        }
    }
}
