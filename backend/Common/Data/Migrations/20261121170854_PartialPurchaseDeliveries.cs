using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartialPurchaseDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "purchase_orders",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_user_id",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "warranty_months",
                table: "purchase_order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receipt_request_id",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_cancelled_by",
                table: "purchase_orders",
                column: "cancelled_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_items_warranty_months",
                table: "purchase_order_items",
                sql: "warranty_months BETWEEN 0 AND 120");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_movements_receipt_request_item",
                table: "inventory_movements",
                columns: new[] { "receipt_request_id", "purchase_order_item_id" },
                unique: true,
                filter: "receipt_request_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_users_cancelled_by_user_id",
                table: "purchase_orders",
                column: "cancelled_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_users_cancelled_by_user_id",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "idx_purchase_orders_cancelled_by",
                table: "purchase_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_items_warranty_months",
                table: "purchase_order_items");

            migrationBuilder.DropIndex(
                name: "uq_inventory_movements_receipt_request_item",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "warranty_months",
                table: "purchase_order_items");

            migrationBuilder.DropColumn(
                name: "receipt_request_id",
                table: "inventory_movements");
        }
    }
}
