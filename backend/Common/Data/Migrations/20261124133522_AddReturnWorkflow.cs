using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "after_sale_quarantine",
                table: "serial_imeis",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "sale_order_item_id",
                table: "serial_imeis",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "warranty_case_id",
                table: "serial_imeis",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "case_line_id",
                table: "order_returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "order_returns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "disposition",
                table: "order_returns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "inspected_at",
                table: "order_returns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "inspection_id",
                table: "order_returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_amount",
                table: "order_returns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_amount",
                table: "order_returns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    inspection_return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_operations_inventory_movements_movement_id",
                        column: x => x.movement_id,
                        principalTable: "inventory_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_operations_order_returns_inspection_return_id",
                        column: x => x.inspection_return_id,
                        principalTable: "order_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.order_item_id);
                    table.CheckConstraint("ck_inventory_reservations_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_inventory_reservations_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO inventory_reservations (order_item_id, quantity, state)
                SELECT i.id, i.quantity, 'Held'
                FROM order_items i JOIN orders o ON o.id = i.order_id
                WHERE o.status = 'Processing'
                """);

            migrationBuilder.CreateTable(
                name: "return_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payout_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    bank_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    replacement_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_cases", x => x.id);
                    table.CheckConstraint("ck_return_cases_status", "status IN ('Pending','AwaitingBank','Refunded','Failed')");
                    table.ForeignKey(
                        name: "FK_return_cases_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_cases_orders_replacement_order_id",
                        column: x => x.replacement_order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_cases_users_payout_actor_id",
                        column: x => x.payout_actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_cases_users_requested_by_id",
                        column: x => x.requested_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_case_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_case_lines", x => x.id);
                    table.CheckConstraint("ck_return_case_lines_amount", "quantity > 0 AND net_amount >= 0 AND discount_amount >= 0 AND tax_amount >= 0");
                    table.ForeignKey(
                        name: "FK_return_case_lines_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_case_lines_return_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "return_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_sale_order_item_id",
                table: "serial_imeis",
                column: "sale_order_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_returns_case_line",
                table: "order_returns",
                column: "case_line_id");

            migrationBuilder.CreateIndex(
                name: "uq_order_returns_inspection",
                table: "order_returns",
                column: "inspection_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_inventory_operations_movement_id",
                table: "inventory_operations",
                column: "movement_id");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_operations_inspection_return",
                table: "inventory_operations",
                column: "inspection_return_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_return_case_lines_item",
                table: "return_case_lines",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "uq_return_case_lines_case_item",
                table: "return_case_lines",
                columns: new[] { "case_id", "order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_return_cases_order_id",
                table: "return_cases",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_return_cases_payout_actor_id",
                table: "return_cases",
                column: "payout_actor_id");

            migrationBuilder.CreateIndex(
                name: "idx_return_cases_requested_by_id",
                table: "return_cases",
                column: "requested_by_id");

            migrationBuilder.CreateIndex(
                name: "uq_return_cases_bank_ref",
                table: "return_cases",
                column: "bank_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_return_cases_replacement",
                table: "return_cases",
                column: "replacement_order_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_movements_order_returns_order_return_id",
                table: "inventory_movements",
                column: "order_return_id",
                principalTable: "order_returns",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_returns_return_case_lines_case_line_id",
                table: "order_returns",
                column: "case_line_id",
                principalTable: "return_case_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_serial_imeis_order_items_sale_order_item_id",
                table: "serial_imeis",
                column: "sale_order_item_id",
                principalTable: "order_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_movements_order_returns_order_return_id",
                table: "inventory_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_order_returns_return_case_lines_case_line_id",
                table: "order_returns");

            migrationBuilder.DropForeignKey(
                name: "FK_serial_imeis_order_items_sale_order_item_id",
                table: "serial_imeis");

            migrationBuilder.DropTable(
                name: "inventory_operations");

            migrationBuilder.DropTable(
                name: "inventory_reservations");

            migrationBuilder.DropTable(
                name: "return_case_lines");

            migrationBuilder.DropTable(
                name: "return_cases");

            migrationBuilder.DropIndex(
                name: "idx_serial_imeis_sale_order_item_id",
                table: "serial_imeis");

            migrationBuilder.DropIndex(
                name: "idx_order_returns_case_line",
                table: "order_returns");

            migrationBuilder.DropIndex(
                name: "uq_order_returns_inspection",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "after_sale_quarantine",
                table: "serial_imeis");

            migrationBuilder.DropColumn(
                name: "sale_order_item_id",
                table: "serial_imeis");

            migrationBuilder.DropColumn(
                name: "warranty_case_id",
                table: "serial_imeis");

            migrationBuilder.DropColumn(
                name: "case_line_id",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "disposition",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "inspected_at",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "inspection_id",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "net_amount",
                table: "order_returns");

            migrationBuilder.DropColumn(
                name: "tax_amount",
                table: "order_returns");
        }
    }
}
