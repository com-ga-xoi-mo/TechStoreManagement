using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPricingAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "orders",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "order_discount_amount",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_amount",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_tax_rate_range",
                table: "orders",
                sql: "tax_rate >= 0 AND tax_rate <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_order_discount_ge_zero",
                table: "order_items",
                sql: "order_discount_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_tax_amount_ge_zero",
                table: "order_items",
                sql: "tax_amount >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_tax_rate_range",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_order_discount_ge_zero",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_tax_amount_ge_zero",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "order_discount_amount",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "tax_amount",
                table: "order_items");
        }
    }
}
