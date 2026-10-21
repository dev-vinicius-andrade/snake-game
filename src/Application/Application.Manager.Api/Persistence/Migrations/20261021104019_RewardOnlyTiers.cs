using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardOnlyTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reward_only",
                table: "perk_tiers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reward_only",
                table: "perk_tiers");
        }
    }
}
