using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPosCashShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "closed_by_user_id",
                table: "pos_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closing_note",
                table: "pos_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "expected_closing_balance",
                table: "pos_sessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cash_tendered",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "change_given",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "checkout_key",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_pos_sessions_closed_by_user_id",
                table: "pos_sessions",
                column: "closed_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pos_sessions_closing_ge_zero",
                table: "pos_sessions",
                sql: "closing_balance IS NULL OR closing_balance >= 0");

            migrationBuilder.CreateIndex(
                name: "uq_orders_cashier_checkout_key",
                table: "orders",
                columns: new[] { "cashier_user_id", "checkout_key" },
                unique: true,
                filter: "checkout_key IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_cash_tendered_ge_zero",
                table: "orders",
                sql: "cash_tendered IS NULL OR cash_tendered >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_change_given_ge_zero",
                table: "orders",
                sql: "change_given IS NULL OR change_given >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_pos_sessions_pos_session_id",
                table: "orders",
                column: "pos_session_id",
                principalTable: "pos_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pos_sessions_users_closed_by_user_id",
                table: "pos_sessions",
                column: "closed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_pos_sessions_pos_session_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_pos_sessions_users_closed_by_user_id",
                table: "pos_sessions");

            migrationBuilder.DropIndex(
                name: "idx_pos_sessions_closed_by_user_id",
                table: "pos_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pos_sessions_closing_ge_zero",
                table: "pos_sessions");

            migrationBuilder.DropIndex(
                name: "uq_orders_cashier_checkout_key",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_cash_tendered_ge_zero",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_change_given_ge_zero",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "closed_by_user_id",
                table: "pos_sessions");

            migrationBuilder.DropColumn(
                name: "closing_note",
                table: "pos_sessions");

            migrationBuilder.DropColumn(
                name: "expected_closing_balance",
                table: "pos_sessions");

            migrationBuilder.DropColumn(
                name: "cash_tendered",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "change_given",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "checkout_key",
                table: "orders");
        }
    }
}
