using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVietQrSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_account_name",
                table: "vietqr_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vietqr_bank_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transfer_content = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    transferred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    vietqr_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vietqr_bank_events", x => x.id);
                    table.CheckConstraint("ck_vietqr_events_amount_gt_zero", "amount > 0");
                    table.CheckConstraint("ck_vietqr_events_resolution", "(status = 'Resolved' AND resolved_at IS NOT NULL AND resolved_by_user_id IS NOT NULL AND resolution_note IS NOT NULL) OR (status <> 'Resolved' AND resolved_at IS NULL AND resolved_by_user_id IS NULL AND resolution_note IS NULL)");
                    table.ForeignKey(
                        name: "FK_vietqr_bank_events_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vietqr_bank_events_vietqr_transactions_vietqr_transaction_id",
                        column: x => x.vietqr_transaction_id,
                        principalTable: "vietqr_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_tx_order_id",
                table: "vietqr_transactions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "uq_vietqr_pending_order",
                table: "vietqr_transactions",
                column: "order_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_events_received_at",
                table: "vietqr_bank_events",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_events_resolved_by",
                table: "vietqr_bank_events",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_events_status",
                table: "vietqr_bank_events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_events_transaction_id",
                table: "vietqr_bank_events",
                column: "vietqr_transaction_id");

            migrationBuilder.CreateIndex(
                name: "uq_vietqr_events_provider_transaction",
                table: "vietqr_bank_events",
                columns: new[] { "provider", "provider_transaction_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vietqr_bank_events");

            migrationBuilder.DropIndex(
                name: "idx_vietqr_tx_order_id",
                table: "vietqr_transactions");

            migrationBuilder.DropIndex(
                name: "uq_vietqr_pending_order",
                table: "vietqr_transactions");

            migrationBuilder.DropColumn(
                name: "bank_account_name",
                table: "vietqr_transactions");
        }
    }
}
