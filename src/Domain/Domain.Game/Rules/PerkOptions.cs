using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>One thing a perk does, and how much.</summary>
public sealed record PerkEffectEntry
{
    public PerkEffect Effect { get; init; }
    public double Amount { get; init; }

    /// <summary>
    /// For <see cref="PerkEffect.EventResistance"/>: the one world event it is about (<c>lightning</c>, <c>meteors</c>, <c>mines</c> or
    /// <c>zone</c>). Empty: all of them.
    /// </summary>
    public string? Target { get; init; }
}

/// <summary>
/// A step up of a perk: once a snake has this many foods (counting what kills and bosses are worth too), everything the perk does
/// is <see cref="Multiplier"/> times its amounts. The perk itself is the first tier, at a multiplier of 1.
/// </summary>
public sealed record PerkTier
{
    public int FoodsRequired { get; init; }
    public double Multiplier { get; init; } = 1;

    /// <summary>
    /// A tier that eating never reaches: only a reward's tier surge lifts a perk into it. It must come after every eatable tier of the perk.
    /// </summary>
    public bool RewardOnly { get; init; }
}

/// <summary>A reward a snake earns once it has eaten <see cref="FoodsRequired"/> foods in its life.</summary>
public sealed record PerkDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int FoodsRequired { get; init; }

    /// <summary>A perk that is switched off is kept in the list but nobody gets it.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The steps up after the first tier (the perk as it is), each asking for more foods than the one before. At most <see cref="MaxTiers"/> in all.</summary>
    public List<PerkTier> Tiers { get; init; } = [];

    /// <summary>Most tiers a perk can have, the first included.</summary>
    public const int MaxTiers = 10;

    /// <summary>The tiers of the perk with the first one, the perk itself, put in front, from the lowest to the highest.</summary>
    public IReadOnlyList<PerkTier> TiersInOrder()
        => [new PerkTier { FoodsRequired = FoodsRequired, Multiplier = 1 }, .. Tiers.OrderBy(tier => tier.FoodsRequired)];

    /// <summary>
    /// Seconds a cooldown ability of the perk (shield burst, dash, heal pulse, vacuum burst) takes to be ready again, counted from when it
    /// was used. Needed when the perk has one; 0 for any other perk.
    /// </summary>
    public int CooldownSeconds { get; init; }

    /// <summary>Seconds a shield burst, dash or vacuum burst of the perk lasts once used. A heal pulse is instant.</summary>
    public int DurationSeconds { get; init; }

    /// <summary>Everything the perk does. Several effects can be combined in one perk.</summary>
    public List<PerkEffectEntry> Effects { get; init; } = [];

    /// <summary>The single effect of older configs, still read: used when <see cref="Effects"/> is empty.</summary>
    public PerkEffect? Effect { get; init; }

    /// <summary>The amount that goes with <see cref="Effect"/>.</summary>
    public double Amount { get; init; }

    /// <summary>
    /// The rooms the perk works in, by invite code, name or id. Empty means every room, except for a perk with a
    /// <see cref="Script"/>: that one is off everywhere until rooms are named.
    /// </summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms the perk does not work in, even where <see cref="Rooms"/> would allow it. Same ways to name a room.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    /// <summary>
    /// The icon players see, a Material Design Icons name such as <c>mdi-shield</c>. Empty: the icon of the perk's first effect.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>A sentence for players that says what the perk does, shown in its tooltip. Useful for a perk made of a script.</summary>
    public string? Description { get; init; }

    /// <summary>JavaScript that decides more of what the perk does while it runs in a room. See docs/PERK_SCRIPTS.md.</summary>
    public string? Script { get; init; }

    public bool HasScript => !string.IsNullOrWhiteSpace(Script);

    /// <summary>The effects of the perk: the list, or the single old effect.</summary>
    public IReadOnlyList<PerkEffectEntry> AllEffects()
        => Effects.Count > 0 ? Effects : Effect is { } effect ? [new PerkEffectEntry { Effect = effect, Amount = Amount }] : [];

    /// <summary>True when the perk works in <paramref name="room"/> (null: no room is known).</summary>
    public bool AppliesTo(IRoom? room)
    {
        return Enabled && RoomNames.Allows("perk", Id, Rooms, ExcludedRooms, room, offUntilNamed: HasScript);
    }
}

/// <summary>Tunable perks. See docs/PERKS.md. Bound from the <c>PerkConfiguration</c> section.</summary>
public sealed record PerkOptions
{
    public const string SectionName = "PerkConfiguration";

    public bool Enabled { get; init; } = true;

    /// <summary>Angle between neighbouring shots of one volley.</summary>
    public double ShotSpreadDegrees { get; init; } = 12;

    /// <summary>Time between the shots of a burst.</summary>
    public int BurstIntervalMilliseconds { get; init; } = 80;

    /// <summary>How many foods killing another snake counts for toward perks and their tiers.</summary>
    public int KillFoodsCredit { get; init; } = 3;

    /// <summary>Share (0..1) of its mass a snake keeps when an extra life saves it.</summary>
    public double ExtraLifeKeepShare { get; init; } = 0.5;

    /// <summary>Seconds a snake that an extra life saved cannot be hurt.</summary>
    public int ExtraLifeProtectSeconds { get; init; } = 3;

    /// <summary>Left empty, the built-in perks are used. Configuration lists add to a default list, so none is preset here.</summary>
    public List<PerkDefinition> Perks { get; init; } = [];

    /// <summary>The tiers the built-in perks that have amounts to scale get: a second at two and a half times the foods, a third at five times.</summary>
    private static List<PerkTier> BuiltInTiers(int foodsRequired)
        => [new PerkTier { FoodsRequired = foodsRequired * 5 / 2, Multiplier = 1.5 }, new PerkTier { FoodsRequired = foodsRequired * 5, Multiplier = 2 }];

    public static IReadOnlyList<PerkDefinition> BuiltIn { get; } =
    [
        new() { Id = "magnet", Name = "Magnet", FoodsRequired = 6, Effect = PerkEffect.Magnet, Amount = 60, Tiers = BuiltInTiers(6) },
        new() { Id = "swift", Name = "Swift", FoodsRequired = 8, Effect = PerkEffect.BoostSpeed, Amount = 0.25, Tiers = BuiltInTiers(8) },
        new() { Id = "quick-shot", Name = "Quick shot", FoodsRequired = 12, Effect = PerkEffect.ShotSpeed, Amount = 0.5, Tiers = BuiltInTiers(12) },
        new() { Id = "twin-shot", Name = "Twin shot", FoodsRequired = 15, Effect = PerkEffect.ExtraShots, Amount = 1, Tiers = BuiltInTiers(15) },
        new() { Id = "armor", Name = "Armor", FoodsRequired = 20, Effect = PerkEffect.Armor, Amount = 0.25, Tiers = BuiltInTiers(20) },
        new() { Id = "quick-reload", Name = "Quick reload", FoodsRequired = 25, Effect = PerkEffect.FasterReload, Amount = 0.35, Tiers = BuiltInTiers(25) },
        new() { Id = "burst-shot", Name = "Burst shot", FoodsRequired = 30, Effect = PerkEffect.BurstShot, Amount = 2, Tiers = BuiltInTiers(30) },
        new() { Id = "deep-lungs", Name = "Deep lungs", FoodsRequired = 35, Effect = PerkEffect.EfficientBoost, Amount = 0.5, Tiers = BuiltInTiers(35) },
        new() { Id = "regeneration", Name = "Regeneration", FoodsRequired = 40, Effect = PerkEffect.Regeneration, Amount = 3, Tiers = BuiltInTiers(40) },
        new() { Id = "auto-fire", Name = "Auto fire", FoodsRequired = 45, Effect = PerkEffect.AutoFire, Amount = 1 },
        new() { Id = "triple-shot", Name = "Triple shot", FoodsRequired = 50, Effect = PerkEffect.ExtraShots, Amount = 1 },
        new() { Id = "piercing-shot", Name = "Piercing shot", FoodsRequired = 60, Effect = PerkEffect.PiercingShot, Amount = 1 },
        new() { Id = "turbo", Name = "Turbo", FoodsRequired = 70, Effect = PerkEffect.BoostSpeed, Amount = 0.3, Tiers = BuiltInTiers(70) },
        new() { Id = "dash", Name = "Dash", FoodsRequired = 18, Effect = PerkEffect.Dash, Amount = 0.8, CooldownSeconds = 15, DurationSeconds = 2, Tiers = BuiltInTiers(18) },
        new() { Id = "steady-aim", Name = "Steady aim", FoodsRequired = 22, Effect = PerkEffect.Precision, Amount = 0.4, Tiers = BuiltInTiers(22) },
        new() { Id = "stormproof", Name = "Stormproof", FoodsRequired = 28, Effect = PerkEffect.EventResistance, Amount = 0.3, Tiers = BuiltInTiers(28) },
        new() { Id = "shield-burst", Name = "Shield burst", FoodsRequired = 33, Effect = PerkEffect.ShieldBurst, Amount = 1, CooldownSeconds = 40, DurationSeconds = 3 },
        new() { Id = "heal-pulse", Name = "Heal pulse", FoodsRequired = 38, Effect = PerkEffect.HealPulse, Amount = 60, CooldownSeconds = 25, Tiers = BuiltInTiers(38) },
        new() { Id = "vacuum", Name = "Vacuum", FoodsRequired = 42, Effect = PerkEffect.VacuumBurst, Amount = 220, CooldownSeconds = 30, DurationSeconds = 4, Tiers = BuiltInTiers(42) },
        new() { Id = "second-chance", Name = "Second chance", FoodsRequired = 55, Effect = PerkEffect.ExtraLife, Amount = 1 },
        new() { Id = "plunder", Name = "Plunder", FoodsRequired = 65, Effect = PerkEffect.Plunder, Amount = 0.15, Tiers = BuiltInTiers(65) }
    ];
}

/// <summary>One kind of prize: a rare, valuable item that appears on the map for snakes to fight over.</summary>
public sealed record PrizeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>A kind that is switched off is kept in the list but never appears.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Seconds from one prize of this kind being taken (or lost) to the next appearing.</summary>
    public int SpawnIntervalSeconds { get; init; } = 60;

    /// <summary>Seconds the prize stays on the map when nobody takes it.</summary>
    public int LifetimeSeconds { get; init; } = 45;

    /// <summary>Mass the snake gains by eating it.</summary>
    public long Value { get; init; } = 20;

    /// <summary>How many foods eating it counts for, toward perks.</summary>
    public int FoodsCredit { get; init; } = 8;

    /// <summary>Players needed in the room before a prize appears; alone there is nobody to fight with.</summary>
    public int MinPlayers { get; init; } = 2;

    /// <summary>The id of a perk the snake that eats it gets, whatever the foods it has eaten. Empty: none.</summary>
    public string? GrantsPerk { get; init; }

    /// <summary>What else it gives: a random perk, extra lives, a tier surge, mass, a mend. Off by default.</summary>
    public PerkReward Reward { get; init; } = new();

    /// <summary>How it looks: six hex digits such as #ffd23f.</summary>
    public string Color { get; init; } = "#ffd23f";

    /// <summary>The rooms the prize appears in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not appear in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows("prize", Id, Rooms, ExcludedRooms, room, offUntilNamed: false);

    /// <summary>The ids of the catalog rewards it gives, all of them. See <see cref="GameplayConfig.Rewards"/>.</summary>
    public List<string> RewardIds { get; init; } = [];

    /// <summary>The catalog rewards <see cref="RewardIds"/> name, filled in when the config is normalized. Never written.</summary>
    [JsonIgnore]
    public IReadOnlyList<RewardDefinition> PickedRewards { get; init; } = [];

    /// <summary>What it gives in <paramref name="room"/>: the rewards it picked, but those the room switched off.</summary>
    public PerkReward RewardFor(IRoom? room) => RewardPicks.For(Reward, PickedRewards, room);
}

/// <summary>Tunable prizes. See docs/PERKS.md. Bound from the <c>PrizeConfiguration</c> section.</summary>
public sealed record PrizeOptions
{
    public const string SectionName = "PrizeConfiguration";

    /// <summary>Turns every prize off with one switch.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The kinds of prize. Left empty, the single prize of the fields below is used (older configs).</summary>
    public List<PrizeDefinition> Prizes { get; init; } = [];

    /// <summary>The kinds in force: the list, or the one the older fields below describe.</summary>
    public IReadOnlyList<PrizeDefinition> Definitions()
        => Prizes.Count > 0
            ? Prizes
            : [new PrizeDefinition
            {
                Id = "prize", Name = "Prize", SpawnIntervalSeconds = SpawnIntervalSeconds, LifetimeSeconds = LifetimeSeconds,
                Value = Value, FoodsCredit = FoodsCredit, MinPlayers = MinPlayers
            }];

    // The fields of the single prize of older configs. New ones use Prizes.

    /// <summary>Seconds from one prize being taken (or lost) to the next appearing.</summary>
    public int SpawnIntervalSeconds { get; init; } = 60;

    /// <summary>Seconds the prize stays on the map when nobody takes it.</summary>
    public int LifetimeSeconds { get; init; } = 45;

    /// <summary>Mass the snake gains by eating it.</summary>
    public long Value { get; init; } = 20;

    /// <summary>How many foods eating it counts for, toward perks.</summary>
    public int FoodsCredit { get; init; } = 8;

    /// <summary>Players needed in the room before a prize appears; alone there is nobody to fight with.</summary>
    public int MinPlayers { get; init; } = 2;
}
