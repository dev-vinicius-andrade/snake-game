using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AchievementsAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enabled",
                table: "achievements",
                type: "boolean",
                nullable: false,
                // Every achievement there is stays on: a new column would otherwise be false and switch them all off.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "achievement_texts",
                columns: table => new
                {
                    achievement_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achievement_texts", x => new { x.achievement_id, x.locale });
                    table.ForeignKey(
                        name: "fk_achievement_texts_achievements",
                        column: x => x.achievement_id,
                        principalTable: "achievements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "achievement_texts");

            migrationBuilder.DropColumn(
                name: "enabled",
                table: "achievements");
        }
    }
}
