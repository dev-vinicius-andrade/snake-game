using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorldEventMaze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_event_maze",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    columns = table.Column<int>(type: "integer", nullable: false),
                    rows = table.Column<int>(type: "integer", nullable: false),
                    corridor_heads = table.Column<int>(type: "integer", nullable: false),
                    twistiness = table.Column<int>(type: "integer", nullable: false),
                    wall_mass_loss = table.Column<long>(type: "bigint", nullable: false),
                    food_percent = table.Column<int>(type: "integer", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    prize_mass = table.Column<long>(type: "bigint", nullable: false),
                    prize_foods_credit = table.Column<int>(type: "integer", nullable: false),
                    prize_grants_perk = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    prize_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_maze", x => x.id);
                    table.ForeignKey(
                        name: "fk_world_event_maze_perks",
                        column: x => x.prize_grants_perk,
                        principalTable: "perks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_world_event_maze_prize_grants_perk",
                table: "world_event_maze",
                column: "prize_grants_perk");

            // A database with a saved config gets the maze at its defaults (the values of MazeOptions when this was written), so nothing is
            // missing until an admin saves. Rooms: none (every room), so world_event_rooms needs no rows. On an empty database this row is
            // cleared and rewritten when the store seeds the config.
            migrationBuilder.InsertData(
                table: "world_event_maze",
                columns: new[]
                {
                    "id", "enabled", "min_interval_seconds", "max_interval_seconds", "duration_seconds", "columns", "rows", "corridor_heads",
                    "twistiness", "wall_mass_loss", "food_percent", "min_players", "prize_mass", "prize_foods_credit", "prize_grants_perk", "prize_color"
                },
                values: new object[] { 1, true, 420, 900, 240, 12, 8, 4, 60, 2L, 25, 2, 200L, 20, null, "#ffd23f" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Its room rows are only in world_event_rooms (no foreign key): drop them too, or a later Up would see stale rooms.
            migrationBuilder.Sql("DELETE FROM world_event_rooms WHERE event = 'maze';");

            migrationBuilder.DropTable(
                name: "world_event_maze");
        }
    }
}
