using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorldEventsBatchTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_event_bounty",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    reward_mass = table.Column<long>(type: "bigint", nullable: false),
                    foods_credit = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_bounty", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_event_fruit_shower",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    fruits_per_second = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_fruit_shower", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_event_mine_field",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    min_mines = table.Column<int>(type: "integer", nullable: false),
                    max_mines = table.Column<int>(type: "integer", nullable: false),
                    radius = table.Column<int>(type: "integer", nullable: false),
                    trigger_radius = table.Column<int>(type: "integer", nullable: false),
                    arm_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_mine_field", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_event_sugar_rush",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_sugar_rush", x => x.id);
                });

            // A database with a saved config gets each new event at its defaults (the values of the option records when this was written),
            // so nothing is missing until an admin saves. Rooms: none (every room), so world_event_rooms needs no rows. On an empty database
            // these rows are cleared and rewritten when the store seeds the config.
            migrationBuilder.InsertData(
                table: "world_event_fruit_shower",
                columns: new[] { "id", "enabled", "min_interval_seconds", "max_interval_seconds", "duration_seconds", "fruits_per_second", "min_players" },
                values: new object[] { 1, true, 180, 420, 20, 8, 1 });

            migrationBuilder.InsertData(
                table: "world_event_bounty",
                columns: new[] { "id", "enabled", "min_interval_seconds", "max_interval_seconds", "duration_seconds", "reward_mass", "foods_credit", "min_players" },
                values: new object[] { 1, true, 240, 600, 90, 150L, 10, 3 });

            migrationBuilder.InsertData(
                table: "world_event_mine_field",
                columns: new[] { "id", "enabled", "min_interval_seconds", "max_interval_seconds", "min_mines", "max_mines", "radius", "trigger_radius", "arm_milliseconds", "min_players" },
                values: new object[] { 1, true, 150, 360, 6, 14, 60, 20, 2000, 1 });

            migrationBuilder.InsertData(
                table: "world_event_sugar_rush",
                columns: new[] { "id", "enabled", "min_interval_seconds", "max_interval_seconds", "duration_seconds", "min_players" },
                values: new object[] { 1, true, 240, 600, 30, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Their room rows are only in world_event_rooms (no foreign key): drop them too, or a later Up would see stale rooms.
            migrationBuilder.Sql("DELETE FROM world_event_rooms WHERE event IN ('fruit_shower', 'bounty', 'mine_field', 'sugar_rush');");

            migrationBuilder.DropTable(
                name: "world_event_bounty");

            migrationBuilder.DropTable(
                name: "world_event_fruit_shower");

            migrationBuilder.DropTable(
                name: "world_event_mine_field");

            migrationBuilder.DropTable(
                name: "world_event_sugar_rush");
        }
    }
}
