using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveReturnDeliveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "was_undelivered",
                table: "return_cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            migrationBuilder.Sql("""
                UPDATE return_cases r SET was_undelivered = true
                FROM orders o
                WHERE o.id = r.order_id AND o.paid_at IS NOT NULL
                  AND (o.completed_at IS NULL OR o.completed_at > r.created_at)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "was_undelivered",
                table: "return_cases");
        }
    }
}
