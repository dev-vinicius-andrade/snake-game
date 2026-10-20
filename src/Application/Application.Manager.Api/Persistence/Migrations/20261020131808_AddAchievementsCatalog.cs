using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "achievements",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    icon = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    stat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    threshold = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achievements", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_unlocked_achievements_achievement_id",
                table: "unlocked_achievements",
                column: "achievement_id");

            migrationBuilder.AddForeignKey(
                name: "fk_unlocked_achievements_achievements",
                table: "unlocked_achievements",
                column: "achievement_id",
                principalTable: "achievements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_unlocked_achievements_achievements",
                table: "unlocked_achievements");

            migrationBuilder.DropTable(
                name: "achievements");

            migrationBuilder.DropIndex(
                name: "ix_unlocked_achievements_achievement_id",
                table: "unlocked_achievements");
        }
    }
}
