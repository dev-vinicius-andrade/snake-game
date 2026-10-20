using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GameplayConfigTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gameplay_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    perks_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    shot_spread_degrees = table.Column<double>(type: "double precision", nullable: false),
                    burst_interval_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    kill_foods_credit = table.Column<int>(type: "integer", nullable: false),
                    prizes_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    empty_private_room_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_private_room_minutes = table.Column<int>(type: "integer", nullable: false),
                    world_events_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gameplay_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perks",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    foods_required = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    icon = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    script = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_event_lightning",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    radius = table.Column<int>(type: "integer", nullable: false),
                    telegraph_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    min_strikes = table.Column<int>(type: "integer", nullable: true),
                    max_strikes = table.Column<int>(type: "integer", nullable: true),
                    storm_seconds = table.Column<int>(type: "integer", nullable: true),
                    aimed_share = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_lightning", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_event_meteors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    radius = table.Column<int>(type: "integer", nullable: false),
                    telegraph_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    min_meteors = table.Column<int>(type: "integer", nullable: false),
                    max_meteors = table.Column<int>(type: "integer", nullable: false),
                    rain_seconds = table.Column<int>(type: "integer", nullable: false),
                    aimed_share = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_meteors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_event_rooms",
                columns: table => new
                {
                    @event = table.Column<string>(name: "event", type: "character varying(32)", maxLength: 32, nullable: false),
                    is_excluded = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    room = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_rooms", x => new { x.@event, x.is_excluded, x.position });
                });

            migrationBuilder.CreateTable(
                name: "world_event_zone",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    warning_seconds = table.Column<int>(type: "integer", nullable: false),
                    shrink_seconds = table.Column<int>(type: "integer", nullable: false),
                    hold_seconds = table.Column<int>(type: "integer", nullable: false),
                    final_radius = table.Column<int>(type: "integer", nullable: false),
                    damage_interval_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    damage_mass = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_zone", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bosses",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    lifetime_seconds = table.Column<int>(type: "integer", nullable: false),
                    hit_points = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<int>(type: "integer", nullable: false),
                    fire_interval_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    arms = table.Column<int>(type: "integer", nullable: false),
                    spin_degrees = table.Column<double>(type: "double precision", nullable: false),
                    shot_speed = table.Column<double>(type: "double precision", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    reward_mass = table.Column<long>(type: "bigint", nullable: false),
                    grants_perk = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    foods_credit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bosses", x => x.id);
                    table.ForeignKey(
                        name: "fk_bosses_perks",
                        column: x => x.grants_perk,
                        principalTable: "perks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "perk_effects",
                columns: table => new
                {
                    perk_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    effect = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perk_effects", x => new { x.perk_id, x.position });
                    table.ForeignKey(
                        name: "fk_perk_effects_perks",
                        column: x => x.perk_id,
                        principalTable: "perks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "perk_rooms",
                columns: table => new
                {
                    perk_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_excluded = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    room = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perk_rooms", x => new { x.perk_id, x.is_excluded, x.position });
                    table.ForeignKey(
                        name: "fk_perk_rooms_perks",
                        column: x => x.perk_id,
                        principalTable: "perks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "perk_tiers",
                columns: table => new
                {
                    perk_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    foods_required = table.Column<int>(type: "integer", nullable: false),
                    multiplier = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perk_tiers", x => new { x.perk_id, x.position });
                    table.ForeignKey(
                        name: "fk_perk_tiers_perks",
                        column: x => x.perk_id,
                        principalTable: "perks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prizes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    spawn_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    lifetime_seconds = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false),
                    foods_credit = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    grants_perk = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prizes", x => x.id);
                    table.ForeignKey(
                        name: "fk_prizes_perks",
                        column: x => x.grants_perk,
                        principalTable: "perks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prize_rooms",
                columns: table => new
                {
                    prize_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_excluded = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    room = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prize_rooms", x => new { x.prize_id, x.is_excluded, x.position });
                    table.ForeignKey(
                        name: "fk_prize_rooms_prizes",
                        column: x => x.prize_id,
                        principalTable: "prizes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bosses_grants_perk",
                table: "bosses",
                column: "grants_perk");

            migrationBuilder.CreateIndex(
                name: "ix_prizes_grants_perk",
                table: "prizes",
                column: "grants_perk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bosses");

            migrationBuilder.DropTable(
                name: "gameplay_settings");

            migrationBuilder.DropTable(
                name: "perk_effects");

            migrationBuilder.DropTable(
                name: "perk_rooms");

            migrationBuilder.DropTable(
                name: "perk_tiers");

            migrationBuilder.DropTable(
                name: "prize_rooms");

            migrationBuilder.DropTable(
                name: "world_event_lightning");

            migrationBuilder.DropTable(
                name: "world_event_meteors");

            migrationBuilder.DropTable(
                name: "world_event_rooms");

            migrationBuilder.DropTable(
                name: "world_event_zone");

            migrationBuilder.DropTable(
                name: "prizes");

            migrationBuilder.DropTable(
                name: "perks");
        }
    }
}
