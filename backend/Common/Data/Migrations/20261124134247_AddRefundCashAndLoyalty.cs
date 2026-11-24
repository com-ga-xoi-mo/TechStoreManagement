using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundCashAndLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cash_refund_total",
                table: "pos_sessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE pos_sessions AS session
                SET cash_refund_total = paid.total
                FROM (
                    SELECT refund_pos_session_id, SUM(refund_amount) AS total
                    FROM order_returns
                    WHERE refund_method = 'Cash' AND refund_pos_session_id IS NOT NULL
                    GROUP BY refund_pos_session_id
                ) AS paid
                WHERE session.id = paid.refund_pos_session_id
                """);

            migrationBuilder.AddColumn<bool>(
                name: "is_sale_accrual",
                table: "customer_loyalty_points",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "refund_case_id",
                table: "customer_loyalty_points",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_refund_payouts",
                columns: table => new
                {
                    refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_refund_payouts", x => x.refund_id);
                    table.CheckConstraint("ck_cash_refund_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "FK_cash_refund_payouts_pos_sessions_shift_id",
                        column: x => x.shift_id,
                        principalTable: "pos_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_refund_payouts_return_cases_refund_id",
                        column: x => x.refund_id,
                        principalTable: "return_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pos_sessions_cash_refunds_ge_zero",
                table: "pos_sessions",
                sql: "cash_refund_total >= 0");

            migrationBuilder.CreateIndex(
                name: "uq_cust_loyalty_refund_case",
                table: "customer_loyalty_points",
                column: "refund_case_id",
                unique: true,
                filter: "refund_case_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_cust_loyalty_sale_order",
                table: "customer_loyalty_points",
                column: "order_id",
                unique: true,
                filter: "is_sale_accrual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cust_loyalty_source",
                table: "customer_loyalty_points",
                sql: "(NOT is_sale_accrual OR (order_id IS NOT NULL AND refund_case_id IS NULL AND points_change > 0)) AND (refund_case_id IS NULL OR (order_id IS NOT NULL AND points_change < 0))");

            migrationBuilder.CreateIndex(
                name: "idx_cash_refund_payouts_shift",
                table: "cash_refund_payouts",
                column: "shift_id");

            migrationBuilder.AddForeignKey(
                name: "FK_customer_loyalty_points_return_cases_refund_case_id",
                table: "customer_loyalty_points",
                column: "refund_case_id",
                principalTable: "return_cases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customer_loyalty_points_return_cases_refund_case_id",
                table: "customer_loyalty_points");

            migrationBuilder.DropTable(
                name: "cash_refund_payouts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pos_sessions_cash_refunds_ge_zero",
                table: "pos_sessions");

            migrationBuilder.DropIndex(
                name: "uq_cust_loyalty_refund_case",
                table: "customer_loyalty_points");

            migrationBuilder.DropIndex(
                name: "uq_cust_loyalty_sale_order",
                table: "customer_loyalty_points");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cust_loyalty_source",
                table: "customer_loyalty_points");

            migrationBuilder.DropColumn(
                name: "cash_refund_total",
                table: "pos_sessions");

            migrationBuilder.DropColumn(
                name: "is_sale_accrual",
                table: "customer_loyalty_points");

            migrationBuilder.DropColumn(
                name: "refund_case_id",
                table: "customer_loyalty_points");
        }
    }
}
