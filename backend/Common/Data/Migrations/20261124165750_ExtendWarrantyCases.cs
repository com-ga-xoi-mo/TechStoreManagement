using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendWarrantyCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_warranty_cases_outcome",
                table: "warranty_cases");

            migrationBuilder.AddColumn<string>(
                name: "case_code",
                table: "warranty_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_name",
                table: "warranty_cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "warranty_cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inspection_note",
                table: "warranty_cases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issue_description",
                table: "warranty_cases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "warranty_cases",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "warranty_case_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_case_events", x => x.id);
                    table.CheckConstraint("ck_warranty_case_events_from_status", "from_status IN ('Received','Repairing','PendingReplacement','Repaired','Rejected','ReturnedToCustomer','Replaced')");
                    table.CheckConstraint("ck_warranty_case_events_to_status", "to_status IN ('Received','Repairing','PendingReplacement','Repaired','Rejected','ReturnedToCustomer','Replaced')");
                    table.ForeignKey(
                        name: "FK_warranty_case_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_case_events_warranty_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "warranty_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE warranty_cases SET status = 'Received' WHERE status = 'UnderRepair';

                WITH numbered AS (
                    SELECT id, to_char(opened_at AT TIME ZONE 'UTC', 'YYYYMM') AS month,
                           row_number() OVER (
                               PARTITION BY date_trunc('month', opened_at AT TIME ZONE 'UTC')
                               ORDER BY opened_at, id
                           )::text AS sequence
                    FROM warranty_cases
                )
                UPDATE warranty_cases AS claim
                SET case_code = 'BH-' || numbered.month || '-' ||
                    lpad(numbered.sequence, greatest(4, length(numbered.sequence)), '0')
                FROM numbered
                WHERE claim.id = numbered.id;

                INSERT INTO warranty_case_events (id, case_id, from_status, to_status, actor_id, created_at)
                SELECT gen_random_uuid(), id, NULL, 'Received', opened_by_id, opened_at
                FROM warranty_cases
                UNION ALL
                SELECT gen_random_uuid(), id, 'Received', status, closed_by_id, closed_at
                FROM warranty_cases
                WHERE status IN ('ReturnedToCustomer', 'Replaced');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "case_code",
                table: "warranty_cases",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_warranty_cases_code",
                table: "warranty_cases",
                column: "case_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_warranty_cases_open_device",
                table: "warranty_cases",
                column: "device_id",
                unique: true,
                filter: "status IN ('Received','Repairing','PendingReplacement','Repaired','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warranty_cases_outcome",
                table: "warranty_cases",
                sql: "(status IN ('Received','Repairing','PendingReplacement','Repaired','Rejected') AND closed_by_id IS NULL AND closed_at IS NULL AND resolution_operation_id IS NULL\n    AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL\n    AND (status <> 'Rejected' OR rejection_reason IS NOT NULL))\nOR (status IN ('ReturnedToCustomer','Replaced') AND closed_by_id IS NOT NULL AND closed_at IS NOT NULL AND resolution_operation_id IS NOT NULL\n    AND ((status = 'ReturnedToCustomer' AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)\n      OR (status = 'Replaced' AND replacement_device_id IS NOT NULL AND replacement_device_id <> device_id AND movement_id IS NOT NULL)))");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_case_events_actor_id",
                table: "warranty_case_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "idx_warranty_case_events_case_id",
                table: "warranty_case_events",
                columns: new[] { "case_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "warranty_case_events");

            migrationBuilder.DropIndex(
                name: "idx_warranty_cases_code",
                table: "warranty_cases");

            migrationBuilder.DropIndex(
                name: "uq_warranty_cases_open_device",
                table: "warranty_cases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_warranty_cases_outcome",
                table: "warranty_cases");

            migrationBuilder.DropColumn(
                name: "case_code",
                table: "warranty_cases");

            migrationBuilder.DropColumn(
                name: "contact_name",
                table: "warranty_cases");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "warranty_cases");

            migrationBuilder.DropColumn(
                name: "inspection_note",
                table: "warranty_cases");

            migrationBuilder.DropColumn(
                name: "issue_description",
                table: "warranty_cases");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "warranty_cases");

            migrationBuilder.Sql("""
                UPDATE warranty_cases SET status = 'UnderRepair'
                WHERE status IN ('Received','Repairing','PendingReplacement','Repaired','Rejected');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_warranty_cases_outcome",
                table: "warranty_cases",
                sql: "(status = 'UnderRepair' AND closed_by_id IS NULL AND closed_at IS NULL AND resolution_operation_id IS NULL\n    AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)\nOR (status IN ('ReturnedToCustomer','Replaced') AND closed_by_id IS NOT NULL AND closed_at IS NOT NULL AND resolution_operation_id IS NOT NULL\n    AND ((status = 'ReturnedToCustomer' AND replacement_device_id IS NULL AND movement_id IS NULL AND replacement_unit_cost IS NULL)\n      OR (status = 'Replaced' AND replacement_device_id IS NOT NULL AND replacement_device_id <> device_id AND movement_id IS NOT NULL)))");
        }
    }
}
