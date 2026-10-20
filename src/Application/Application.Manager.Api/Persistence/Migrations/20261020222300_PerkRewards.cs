using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerkRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "reward_bonus_mass",
                table: "world_event_maze",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "reward_extra_lives",
                table: "world_event_maze",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "reward_mend",
                table: "world_event_maze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reward_random_perk",
                table: "world_event_maze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_max",
                table: "world_event_maze",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_min",
                table: "world_event_maze",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reward_tier_surge_perk",
                table: "world_event_maze",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reward_bonus_mass",
                table: "world_event_bounty",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "reward_extra_lives",
                table: "world_event_bounty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "reward_mend",
                table: "world_event_bounty",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reward_random_perk",
                table: "world_event_bounty",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_max",
                table: "world_event_bounty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_min",
                table: "world_event_bounty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reward_tier_surge_perk",
                table: "world_event_bounty",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reward_bonus_mass",
                table: "prizes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "reward_extra_lives",
                table: "prizes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "reward_mend",
                table: "prizes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reward_random_perk",
                table: "prizes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_max",
                table: "prizes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_min",
                table: "prizes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reward_tier_surge_perk",
                table: "prizes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cooldown_seconds",
                table: "perks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "duration_seconds",
                table: "perks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "target",
                table: "perk_effects",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // The generated defaults (0 and 0) would give a saved config a snake that keeps no mass and is not protected after an
            // extra life. The game's own defaults are 0.5 and 3 seconds, so the row that already exists keeps them.
            migrationBuilder.AddColumn<double>(
                name: "extra_life_keep_share",
                table: "gameplay_settings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<int>(
                name: "extra_life_protect_seconds",
                table: "gameplay_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<long>(
                name: "reward_bonus_mass",
                table: "bosses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "reward_extra_lives",
                table: "bosses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "reward_mend",
                table: "bosses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reward_random_perk",
                table: "bosses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_max",
                table: "bosses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reward_tier_surge_min",
                table: "bosses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reward_tier_surge_perk",
                table: "bosses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reward_bonus_mass",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_extra_lives",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_mend",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_random_perk",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_max",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_min",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_perk",
                table: "world_event_maze");

            migrationBuilder.DropColumn(
                name: "reward_bonus_mass",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_extra_lives",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_mend",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_random_perk",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_max",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_min",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_perk",
                table: "world_event_bounty");

            migrationBuilder.DropColumn(
                name: "reward_bonus_mass",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "reward_extra_lives",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "reward_mend",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "reward_random_perk",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_max",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_min",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_perk",
                table: "prizes");

            migrationBuilder.DropColumn(
                name: "cooldown_seconds",
                table: "perks");

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "perks");

            migrationBuilder.DropColumn(
                name: "target",
                table: "perk_effects");

            migrationBuilder.DropColumn(
                name: "extra_life_keep_share",
                table: "gameplay_settings");

            migrationBuilder.DropColumn(
                name: "extra_life_protect_seconds",
                table: "gameplay_settings");

            migrationBuilder.DropColumn(
                name: "reward_bonus_mass",
                table: "bosses");

            migrationBuilder.DropColumn(
                name: "reward_extra_lives",
                table: "bosses");

            migrationBuilder.DropColumn(
                name: "reward_mend",
                table: "bosses");

            migrationBuilder.DropColumn(
                name: "reward_random_perk",
                table: "bosses");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_max",
                table: "bosses");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_min",
                table: "bosses");

            migrationBuilder.DropColumn(
                name: "reward_tier_surge_perk",
                table: "bosses");
        }
    }
}
