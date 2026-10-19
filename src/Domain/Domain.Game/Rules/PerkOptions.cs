using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>One thing a perk does, and how much.</summary>
public sealed record PerkEffectEntry
{
    public PerkEffect Effect { get; init; }
    public double Amount { get; init; }
}

/// <summary>A reward a snake earns once it has eaten <see cref="FoodsRequired"/> foods in its life.</summary>
public sealed record PerkDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int FoodsRequired { get; init; }

    /// <summary>A perk that is switched off is kept in the list but nobody gets it.</summary>
    public bool Enabled { get; init; } = true;

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

    /// <summary>JavaScript that decides more of what the perk does while it runs in a room. See docs/PERK_SCRIPTS.md.</summary>
    public string? Script { get; init; }

    public bool HasScript => !string.IsNullOrWhiteSpace(Script);

    /// <summary>The effects of the perk: the list, or the single old effect.</summary>
    public IReadOnlyList<PerkEffectEntry> AllEffects()
        => Effects.Count > 0 ? Effects : Effect is { } effect ? [new PerkEffectEntry { Effect = effect, Amount = Amount }] : [];

    /// <summary>True when the perk works in <paramref name="room"/> (null: no room is known).</summary>
    public bool AppliesTo(IRoom? room)
    {
        return Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: HasScript);
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

    /// <summary>Left empty, the built-in perks are used. Configuration lists add to a default list, so none is preset here.</summary>
    public List<PerkDefinition> Perks { get; init; } = [];

    public static IReadOnlyList<PerkDefinition> BuiltIn { get; } =
    [
        new() { Id = "magnet", Name = "Magnet", FoodsRequired = 6, Effect = PerkEffect.Magnet, Amount = 60 },
        new() { Id = "swift", Name = "Swift", FoodsRequired = 8, Effect = PerkEffect.BoostSpeed, Amount = 0.25 },
        new() { Id = "quick-shot", Name = "Quick shot", FoodsRequired = 12, Effect = PerkEffect.ShotSpeed, Amount = 0.5 },
        new() { Id = "twin-shot", Name = "Twin shot", FoodsRequired = 15, Effect = PerkEffect.ExtraShots, Amount = 1 },
        new() { Id = "armor", Name = "Armor", FoodsRequired = 20, Effect = PerkEffect.Armor, Amount = 0.25 },
        new() { Id = "quick-reload", Name = "Quick reload", FoodsRequired = 25, Effect = PerkEffect.FasterReload, Amount = 0.35 },
        new() { Id = "burst-shot", Name = "Burst shot", FoodsRequired = 30, Effect = PerkEffect.BurstShot, Amount = 2 },
        new() { Id = "deep-lungs", Name = "Deep lungs", FoodsRequired = 35, Effect = PerkEffect.EfficientBoost, Amount = 0.5 },
        new() { Id = "regeneration", Name = "Regeneration", FoodsRequired = 40, Effect = PerkEffect.Regeneration, Amount = 3 },
        new() { Id = "auto-fire", Name = "Auto fire", FoodsRequired = 45, Effect = PerkEffect.AutoFire, Amount = 1 },
        new() { Id = "triple-shot", Name = "Triple shot", FoodsRequired = 50, Effect = PerkEffect.ExtraShots, Amount = 1 },
        new() { Id = "piercing-shot", Name = "Piercing shot", FoodsRequired = 60, Effect = PerkEffect.PiercingShot, Amount = 1 },
        new() { Id = "turbo", Name = "Turbo", FoodsRequired = 70, Effect = PerkEffect.BoostSpeed, Amount = 0.3 }
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

    /// <summary>How it looks: six hex digits such as #ffd23f.</summary>
    public string Color { get; init; } = "#ffd23f";

    /// <summary>The rooms the prize appears in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not appear in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
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
