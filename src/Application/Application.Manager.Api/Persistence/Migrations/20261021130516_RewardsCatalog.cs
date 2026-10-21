using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Manager.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rewards",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    grants_random_perk = table.Column<bool>(type: "boolean", nullable: false),
                    extra_lives = table.Column<int>(type: "integer", nullable: false),
                    tier_surge_min = table.Column<int>(type: "integer", nullable: false),
                    tier_surge_max = table.Column<int>(type: "integer", nullable: false),
                    tier_surge_perk = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    bonus_mass = table.Column<long>(type: "bigint", nullable: false),
                    mend = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rewards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prize_rewards",
                columns: table => new
                {
                    prize_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    reward_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prize_rewards", x => new { x.prize_id, x.position });
                    table.ForeignKey(
                        name: "fk_prize_rewards_prizes",
                        column: x => x.prize_id,
                        principalTable: "prizes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_prize_rewards_rewards",
                        column: x => x.reward_id,
                        principalTable: "rewards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "world_event_rewards",
                columns: table => new
                {
                    @event = table.Column<string>(name: "event", type: "character varying(32)", maxLength: 32, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    reward_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_event_rewards", x => new { x.@event, x.position });
                    table.ForeignKey(
                        name: "fk_world_event_rewards_rewards",
                        column: x => x.reward_id,
                        principalTable: "rewards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prize_rewards_reward_id",
                table: "prize_rewards",
                column: "reward_id");

            migrationBuilder.CreateIndex(
                name: "ix_world_event_rewards_reward_id",
                table: "world_event_rewards",
                column: "reward_id");
            // The rewards written inside a prize, the boss, the bounty and the maze become catalog entries those pick,
            // so nothing is lost. Only rows that give something are moved: an all-off reward gives nothing and was never a reward.
            // The ids and names are the ones GameplayConfig.WithRewardsResolved would make for a config saved before the catalog.
            migrationBuilder.Sql(@"
INSERT INTO rewards (id, name, grants_random_perk, extra_lives, tier_surge_min, tier_surge_max, tier_surge_perk, bonus_mass, mend, sort_order)
SELECT left('prize-' || id || '-reward', 64), left(name || ' reward', 64), reward_random_perk, reward_extra_lives, reward_tier_surge_min,
       reward_tier_surge_max, reward_tier_surge_perk, reward_bonus_mass, reward_mend, sort_order
FROM prizes
WHERE reward_random_perk OR reward_extra_lives > 0 OR reward_tier_surge_min > 0 OR reward_tier_surge_max > 0 OR reward_bonus_mass > 0 OR reward_mend;

INSERT INTO prize_rewards (prize_id, position, reward_id)
SELECT id, 0, left('prize-' || id || '-reward', 64)
FROM prizes
WHERE reward_random_perk OR reward_extra_lives > 0 OR reward_tier_surge_min > 0 OR reward_tier_surge_max > 0 OR reward_bonus_mass > 0 OR reward_mend;

INSERT INTO rewards (id, name, grants_random_perk, extra_lives, tier_surge_min, tier_surge_max, tier_surge_perk, bonus_mass, mend, sort_order)
SELECT 'turret-reward', 'Turret reward', reward_random_perk, reward_extra_lives, reward_tier_surge_min, reward_tier_surge_max, reward_tier_surge_perk,
       reward_bonus_mass, reward_mend, 1000
FROM bosses
WHERE id = 'turret' AND (reward_random_perk OR reward_extra_lives > 0 OR reward_tier_surge_min > 0 OR reward_tier_surge_max > 0 OR reward_bonus_mass > 0 OR reward_mend);

INSERT INTO world_event_rewards (event, position, reward_id)
SELECT 'turret', 0, 'turret-reward' WHERE EXISTS (SELECT 1 FROM rewards WHERE id = 'turret-reward');

INSERT INTO rewards (id, name, grants_random_perk, extra_lives, tier_surge_min, tier_surge_max, tier_surge_perk, bonus_mass, mend, sort_order)
SELECT 'bounty-reward', 'Bounty reward', reward_random_perk, reward_extra_lives, reward_tier_surge_min, reward_tier_surge_max, reward_tier_surge_perk,
       reward_bonus_mass, reward_mend, 1001
FROM world_event_bounty
WHERE reward_random_perk OR reward_extra_lives > 0 OR reward_tier_surge_min > 0 OR reward_tier_surge_max > 0 OR reward_bonus_mass > 0 OR reward_mend;

INSERT INTO world_event_rewards (event, position, reward_id)
SELECT 'bounty', 0, 'bounty-reward' WHERE EXISTS (SELECT 1 FROM rewards WHERE id = 'bounty-reward');

INSERT INTO rewards (id, name, grants_random_perk, extra_lives, tier_surge_min, tier_surge_max, tier_surge_perk, bonus_mass, mend, sort_order)
SELECT 'maze-reward', 'Maze prize reward', reward_random_perk, reward_extra_lives, reward_tier_surge_min, reward_tier_surge_max, reward_tier_surge_perk,
       reward_bonus_mass, reward_mend, 1002
FROM world_event_maze
WHERE reward_random_perk OR reward_extra_lives > 0 OR reward_tier_surge_min > 0 OR reward_tier_surge_max > 0 OR reward_bonus_mass > 0 OR reward_mend;

INSERT INTO world_event_rewards (event, position, reward_id)
SELECT 'maze', 0, 'maze-reward' WHERE EXISTS (SELECT 1 FROM rewards WHERE id = 'maze-reward');
");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            // Best effort: a prize or event that picked several rewards keeps the first of them in its own columns again.
            migrationBuilder.Sql(@"
UPDATE prizes SET reward_random_perk = r.grants_random_perk, reward_extra_lives = r.extra_lives, reward_tier_surge_min = r.tier_surge_min,
       reward_tier_surge_max = r.tier_surge_max, reward_tier_surge_perk = r.tier_surge_perk, reward_bonus_mass = r.bonus_mass, reward_mend = r.mend
FROM prize_rewards l JOIN rewards r ON r.id = l.reward_id WHERE l.prize_id = prizes.id AND l.position = 0;

UPDATE bosses SET reward_random_perk = r.grants_random_perk, reward_extra_lives = r.extra_lives, reward_tier_surge_min = r.tier_surge_min,
       reward_tier_surge_max = r.tier_surge_max, reward_tier_surge_perk = r.tier_surge_perk, reward_bonus_mass = r.bonus_mass, reward_mend = r.mend
FROM world_event_rewards l JOIN rewards r ON r.id = l.reward_id WHERE l.event = 'turret' AND l.position = 0 AND bosses.id = 'turret';

UPDATE world_event_bounty SET reward_random_perk = r.grants_random_perk, reward_extra_lives = r.extra_lives, reward_tier_surge_min = r.tier_surge_min,
       reward_tier_surge_max = r.tier_surge_max, reward_tier_surge_perk = r.tier_surge_perk, reward_bonus_mass = r.bonus_mass, reward_mend = r.mend
FROM world_event_rewards l JOIN rewards r ON r.id = l.reward_id WHERE l.event = 'bounty' AND l.position = 0;

UPDATE world_event_maze SET reward_random_perk = r.grants_random_perk, reward_extra_lives = r.extra_lives, reward_tier_surge_min = r.tier_surge_min,
       reward_tier_surge_max = r.tier_surge_max, reward_tier_surge_perk = r.tier_surge_perk, reward_bonus_mass = r.bonus_mass, reward_mend = r.mend
FROM world_event_rewards l JOIN rewards r ON r.id = l.reward_id WHERE l.event = 'maze' AND l.position = 0;
");

            migrationBuilder.DropTable(
                name: "prize_rewards");

            migrationBuilder.DropTable(
                name: "world_event_rewards");

            migrationBuilder.DropTable(
                name: "rewards");
        }
    }
}
