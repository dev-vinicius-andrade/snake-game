namespace Application.Manager.Api.Persistence;

/// <summary>The columns of a reward, the same on every table that has one (prizes, the bounty, the maze and the bosses).</summary>
public interface IRewardColumns
{
    bool RewardRandomPerk { get; set; }
    int RewardExtraLives { get; set; }
    int RewardTierSurgeMin { get; set; }
    int RewardTierSurgeMax { get; set; }
    string? RewardTierSurgePerk { get; set; }
    long RewardBonusMass { get; set; }
    bool RewardMend { get; set; }
}

// The gameplay config as tables (docs/DATABASE.md): the perks, the prizes, the world events and the boss, and the few settings they share.
// A save replaces all of it in one transaction, so the tables always hold one whole config, which <see cref="GameplaySettingsRow.Revision"/> names.

/// <summary>The one row (<see cref="Id"/> is 1) of what is not a perk, a prize, an event or a boss: the switches, the room lifetimes and the revision.</summary>
public sealed class GameplaySettingsRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    /// <summary>Changes with every save. A game server applies a revision it has not seen.</summary>
    public string Revision { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }

    public bool PerksEnabled { get; set; }

    public double ShotSpreadDegrees { get; set; }

    public int BurstIntervalMilliseconds { get; set; }

    public int KillFoodsCredit { get; set; }

    /// <summary>Share of its mass a snake keeps when an extra life saves it. Rows from before extra lives hold 0.5 (set by the migration).</summary>
    public double ExtraLifeKeepShare { get; set; }

    /// <summary>Seconds a saved snake cannot be hurt. Rows from before extra lives hold 3 (set by the migration).</summary>
    public int ExtraLifeProtectSeconds { get; set; }

    public bool PrizesEnabled { get; set; }

    public int EmptyPrivateRoomMinutes { get; set; }

    public int MaxPrivateRoomMinutes { get; set; }

    public bool WorldEventsEnabled { get; set; }
}

public sealed class PerkRow
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public int FoodsRequired { get; set; }

    public bool Enabled { get; set; }

    public string? Icon { get; set; }

    public string? Description { get; set; }

    /// <summary>JavaScript that decides more of what the perk does (docs/PERK_SCRIPTS.md).</summary>
    public string? Script { get; set; }

    /// <summary>Seconds a cooldown ability of the perk takes to recharge. 0: the perk has none.</summary>
    public int CooldownSeconds { get; set; }

    /// <summary>Seconds a shield burst, dash or vacuum burst of the perk lasts.</summary>
    public int DurationSeconds { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>One thing a perk does: the effect by name (<c>Magnet</c>, <c>Armor</c>...) and how much.</summary>
public sealed class PerkEffectRow
{
    public string PerkId { get; set; } = "";

    public int Position { get; set; }

    public string Effect { get; set; } = "";

    public double Amount { get; set; }

    /// <summary>For event resistance: the one event it is for (<c>lightning</c>, <c>meteors</c>, <c>mines</c>, <c>zone</c>). Null: all of them.</summary>
    public string? Target { get; set; }
}

/// <summary>A step up of a perk: with this many foods everything it does is multiplied.</summary>
public sealed class PerkTierRow
{
    public string PerkId { get; set; } = "";

    public int Position { get; set; }

    public int FoodsRequired { get; set; }

    public double Multiplier { get; set; }

    /// <summary>A tier that eating never reaches; only a reward's tier surge does. False for every tier saved before it existed.</summary>
    public bool RewardOnly { get; set; }
}

/// <summary>A room a perk is for (or, with <see cref="IsExcluded"/>, not for), by invite code, name or id.</summary>
public sealed class PerkRoomRow
{
    public string PerkId { get; set; } = "";

    public bool IsExcluded { get; set; }

    public int Position { get; set; }

    public string Room { get; set; } = "";
}

public sealed class PrizeRow : IRewardColumns
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Enabled { get; set; }

    public int SpawnIntervalSeconds { get; set; }

    public int LifetimeSeconds { get; set; }

    public long Value { get; set; }

    public int FoodsCredit { get; set; }

    public int MinPlayers { get; set; }

    /// <summary>The perk the snake that eats it gets. Must be in <c>perks</c>.</summary>
    public string? GrantsPerk { get; set; }

    public string Color { get; set; } = "";

    public int SortOrder { get; set; }

    // What else it gives (the reward). All off by default: a row from before rewards gives nothing more.

    public bool RewardRandomPerk { get; set; }

    public int RewardExtraLives { get; set; }

    public int RewardTierSurgeMin { get; set; }

    public int RewardTierSurgeMax { get; set; }

    /// <summary>The perk a tier surge lifts. Empty: a random one. Checked when the config is saved, not by a foreign key.</summary>
    public string? RewardTierSurgePerk { get; set; }

    public long RewardBonusMass { get; set; }

    public bool RewardMend { get; set; }
}

public sealed class PrizeRoomRow
{
    public string PrizeId { get; set; } = "";

    public bool IsExcluded { get; set; }

    public int Position { get; set; }

    public string Room { get; set; } = "";
}

/// <summary>Lightning storms. One row (<see cref="Id"/> is 1). The storm columns are empty for a config from before storms.</summary>
public sealed class LightningEventRow
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int Radius { get; set; }

    public int TelegraphMilliseconds { get; set; }

    public int? MinStrikes { get; set; }

    public int? MaxStrikes { get; set; }

    public int? StormSeconds { get; set; }

    public double AimedShare { get; set; }
}

/// <summary>Meteor rain. One row (<see cref="Id"/> is 1).</summary>
public sealed class MeteorEventRow
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int Radius { get; set; }

    public int TelegraphMilliseconds { get; set; }

    public int MinMeteors { get; set; }

    public int MaxMeteors { get; set; }

    public int RainSeconds { get; set; }

    public double AimedShare { get; set; }
}

/// <summary>The shrinking safe zone. One row (<see cref="Id"/> is 1).</summary>
public sealed class ZoneEventRow
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int WarningSeconds { get; set; }

    public int ShrinkSeconds { get; set; }

    public int HoldSeconds { get; set; }

    public int FinalRadius { get; set; }

    public int DamageIntervalMilliseconds { get; set; }

    public int DamageMass { get; set; }

    public int MinPlayers { get; set; }
}

/// <summary>Fruit shower. One row (<see cref="Id"/> is 1).</summary>
public sealed class FruitShowerEventRow
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int DurationSeconds { get; set; }

    public int FruitsPerSecond { get; set; }

    public int MinPlayers { get; set; }
}

/// <summary>Bounty on a random snake. One row (<see cref="Id"/> is 1).</summary>
public sealed class BountyEventRow : IRewardColumns
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int DurationSeconds { get; set; }

    public long RewardMass { get; set; }

    public int FoodsCredit { get; set; }

    public int MinPlayers { get; set; }

    // What else it gives (the reward). All off by default: a row from before rewards gives nothing more.

    public bool RewardRandomPerk { get; set; }

    public int RewardExtraLives { get; set; }

    public int RewardTierSurgeMin { get; set; }

    public int RewardTierSurgeMax { get; set; }

    /// <summary>The perk a tier surge lifts. Empty: a random one. Checked when the config is saved, not by a foreign key.</summary>
    public string? RewardTierSurgePerk { get; set; }

    public long RewardBonusMass { get; set; }

    public bool RewardMend { get; set; }
}

/// <summary>Mine field. One row (<see cref="Id"/> is 1).</summary>
public sealed class MineFieldEventRow
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int MinMines { get; set; }

    public int MaxMines { get; set; }

    public int Radius { get; set; }

    public int TriggerRadius { get; set; }

    public int ArmMilliseconds { get; set; }

    public int MinPlayers { get; set; }
}

/// <summary>Sugar rush (free boost). One row (<see cref="Id"/> is 1).</summary>
public sealed class SugarRushEventRow
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int DurationSeconds { get; set; }

    public int MinPlayers { get; set; }
}

/// <summary>Maze. One row (<see cref="Id"/> is 1).</summary>
public sealed class MazeEventRow : IRewardColumns
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int DurationSeconds { get; set; }

    public int Columns { get; set; }

    public int Rows { get; set; }

    public int CorridorHeads { get; set; }

    public int Twistiness { get; set; }

    public long WallMassLoss { get; set; }

    public int FoodPercent { get; set; }

    public int MinPlayers { get; set; }

    public long PrizeMass { get; set; }

    public int PrizeFoodsCredit { get; set; }

    /// <summary>The perk the snake that takes the prize gets. Must be in <c>perks</c>.</summary>
    public string? PrizeGrantsPerk { get; set; }

    public string PrizeColor { get; set; } = "#ffd23f";

    // What else it gives (the reward). All off by default: a row from before rewards gives nothing more.

    public bool RewardRandomPerk { get; set; }

    public int RewardExtraLives { get; set; }

    public int RewardTierSurgeMin { get; set; }

    public int RewardTierSurgeMax { get; set; }

    /// <summary>The perk a tier surge lifts. Empty: a random one. Checked when the config is saved, not by a foreign key.</summary>
    public string? RewardTierSurgePerk { get; set; }

    public long RewardBonusMass { get; set; }

    public bool RewardMend { get; set; }
}

/// <summary>A boss. Today the game has one, the turret (<see cref="Id"/> <c>turret</c>); more kinds are more rows.</summary>
public sealed class BossRow : IRewardColumns
{
    public const string Turret = "turret";

    public string Id { get; set; } = Turret;

    public bool Enabled { get; set; }

    public int MinIntervalSeconds { get; set; }

    public int MaxIntervalSeconds { get; set; }

    public int LifetimeSeconds { get; set; }

    public int HitPoints { get; set; }

    public int Size { get; set; }

    public int FireIntervalMilliseconds { get; set; }

    public int Arms { get; set; }

    public double SpinDegrees { get; set; }

    public double ShotSpeed { get; set; }

    public int MinPlayers { get; set; }

    public long RewardMass { get; set; }

    /// <summary>The perk the snake that brings it down gets. Must be in <c>perks</c>.</summary>
    public string? GrantsPerk { get; set; }

    public int FoodsCredit { get; set; }

    // What else it gives (the reward). All off by default: a row from before rewards gives nothing more.

    public bool RewardRandomPerk { get; set; }

    public int RewardExtraLives { get; set; }

    public int RewardTierSurgeMin { get; set; }

    public int RewardTierSurgeMax { get; set; }

    /// <summary>The perk a tier surge lifts. Empty: a random one. Checked when the config is saved, not by a foreign key.</summary>
    public string? RewardTierSurgePerk { get; set; }

    public long RewardBonusMass { get; set; }

    public bool RewardMend { get; set; }
}

/// <summary>A room a world event (<c>lightning</c>, <c>meteors</c>, <c>zone</c>, <c>fruit_shower</c>, <c>bounty</c>, <c>mine_field</c>, <c>sugar_rush</c>, <c>maze</c>) or a boss (<c>turret</c>) is for, or with <see cref="IsExcluded"/> not for.</summary>
public sealed class WorldEventRoomRow
{
    public string Event { get; set; } = "";

    public bool IsExcluded { get; set; }

    public int Position { get; set; }

    public string Room { get; set; } = "";
}
