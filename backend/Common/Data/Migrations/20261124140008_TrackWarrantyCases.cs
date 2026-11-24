using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackWarrantyCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warranty_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    warranty_start_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    warranty_end_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    receive_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolution_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    replacement_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    replacement_unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_cases", x => x.id);
                    table.CheckConstraint("ck_warranty_cases_cost", "replacement_unit_cost IS NULL OR replacement_unit_cost >= 0");
                    table.CheckConstraint("ck_warranty_cases_dates", "warranty_start_at <= opened_at AND opened_at < warranty_end_at AND (closed_at IS NULL OR closed_at >= opened_at)");
                    table.CheckConstraint("ck_warranty_cases_outcome", "(status = 'UnderRepair' AND closed_by_id IS NULL AND closed_at IS NULL AND resolution_operation_id IS NULL\n    AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)\nOR (status IN ('ReturnedToCustomer','Replaced') AND closed_by_id IS NOT NULL AND closed_at IS NOT NULL AND resolution_operation_id IS NOT NULL\n    AND ((status = 'ReturnedToCustomer' AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)\n      OR (status = 'Replaced' AND replacement_device_id IS NOT NULL AND replacement_device_id <> device_id AND movement_id IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_warranty_cases_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_inventory_movements_movement_id",
                        column: x => x.movement_id,
                        principalTable: "inventory_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_inventory_operations_receive_operation_id",
                        column: x => x.receive_operation_id,
                        principalTable: "inventory_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_inventory_operations_resolution_operation_id",
                        column: x => x.resolution_operation_id,
                        principalTable: "inventory_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_order_items_order_item_id_order_id",
                        columns: x => new { x.order_item_id, x.order_id },
                        principalTable: "order_items",
                        principalColumns: new[] { "id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_serial_imeis_device_id",
                        column: x => x.device_id,
                        principalTable: "serial_imeis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_serial_imeis_replacement_device_id",
                        column: x => x.replacement_device_id,
                        principalTable: "serial_imeis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_users_closed_by_id",
                        column: x => x.closed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_cases_users_opened_by_id",
                        column: x => x.opened_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_closed_by",
                table: "warranty_cases",
                column: "closed_by_id");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_customer",
                table: "warranty_cases",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_device",
                table: "warranty_cases",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_item_order",
                table: "warranty_cases",
                columns: new[] { "order_item_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_opened_by",
                table: "warranty_cases",
                column: "opened_by_id");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_order",
                table: "warranty_cases",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_replacement",
                table: "warranty_cases",
                column: "replacement_device_id");

            migrationBuilder.CreateIndex(
                name: "uq_warranty_cases_movement",
                table: "warranty_cases",
                column: "movement_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_warranty_cases_receive_op",
                table: "warranty_cases",
                column: "receive_operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_warranty_cases_resolution_op",
                table: "warranty_cases",
                column: "resolution_operation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "warranty_cases");
        }
    }
}
