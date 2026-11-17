using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceiptFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "client_request_id",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_document_number",
                table: "purchase_orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_client_request_id",
                table: "purchase_orders",
                column: "client_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_purchase_orders_client_request_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "client_request_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "supplier_document_number",
                table: "purchase_orders");
        }
    }
}
