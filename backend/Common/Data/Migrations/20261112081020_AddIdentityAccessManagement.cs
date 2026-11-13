using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAccessManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $identity$
                DECLARE conflicts text;
                BEGIN
                    SELECT string_agg(id::text, ', ') INTO conflicts FROM users
                    WHERE lower(btrim(username)) IN
                        (SELECT lower(btrim(username)) FROM users GROUP BY lower(btrim(username)) HAVING count(*) > 1)
                       OR lower(btrim(email)) IN
                        (SELECT lower(btrim(email)) FROM users GROUP BY lower(btrim(email)) HAVING count(*) > 1);
                    IF conflicts IS NOT NULL THEN
                        RAISE EXCEPTION 'Identity preflight: duplicate normalized username/email; resolve user IDs: %', conflicts;
                    END IF;
                    IF EXISTS (SELECT 1 FROM users) AND NOT EXISTS (
                        SELECT 1 FROM users u JOIN user_roles ur ON ur.user_id = u.id
                        JOIN roles r ON r.id = ur.role_id
                        WHERE u.is_active AND u.password_hash <> '' AND r.name = 'Admin'
                          AND (SELECT count(*) FROM user_roles all_roles WHERE all_roles.user_id = u.id) = 1
                    ) THEN
                        RAISE EXCEPTION 'Identity preflight: no active sole-role Admin; restore an administrative identity before upgrading';
                    END IF;
                END $identity$;
                CREATE UNIQUE INDEX idx_users_normalized_username ON users (lower(btrim(username)));
                CREATE UNIQUE INDEX idx_users_normalized_email ON users (lower(btrim(email)));
                """);

            migrationBuilder.AddColumn<long>(
                name: "token_version",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "chk_users_token_version",
                table: "users",
                sql: "token_version >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX idx_users_normalized_username; DROP INDEX idx_users_normalized_email;");

            migrationBuilder.DropCheckConstraint(
                name: "chk_users_token_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "users");
        }
    }
}
