using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    username_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tag = table.Column<int>(type: "integer", nullable: false),
                    phrase_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "applied_reports",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applied_reports", x => new { x.account_id, x.report_id });
                });

            migrationBuilder.CreateTable(
                name: "stat_values",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stat_values", x => new { x.account_id, x.kind, x.key });
                });

            migrationBuilder.CreateTable(
                name: "stats_heads",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stats_heads", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "unlocked_achievements",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    achievement_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unlocked_achievements", x => new { x.account_id, x.achievement_id });
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.hash);
                    table.ForeignKey(
                        name: "fk_devices_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_logins",
                columns: table => new
                {
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    id_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_logins", x => new { x.provider, x.id_hash });
                    table.ForeignKey(
                        name: "fk_external_logins_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_accounts_username_key_tag",
                table: "accounts",
                columns: new[] { "username_key", "tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applied_reports_account_id_sequence",
                table: "applied_reports",
                columns: new[] { "account_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_account_id",
                table: "devices",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ux_external_logins_account_id_provider",
                table: "external_logins",
                columns: new[] { "account_id", "provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applied_reports");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "external_logins");

            migrationBuilder.DropTable(
                name: "stat_values");

            migrationBuilder.DropTable(
                name: "stats_heads");

            migrationBuilder.DropTable(
                name: "unlocked_achievements");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
