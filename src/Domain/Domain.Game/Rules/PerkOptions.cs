namespace Domain.Game.Rules;

/// <summary>A reward a snake earns once it has eaten <see cref="FoodsRequired"/> foods in its life.</summary>
public sealed record PerkDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int FoodsRequired { get; init; }
    public PerkEffect Effect { get; init; }
    public double Amount { get; init; }
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

/// <summary>Tunable prize: a rare, valuable item that appears on the map for snakes to fight over.</summary>
public sealed record PrizeOptions
{
    public const string SectionName = "PrizeConfiguration";

    public bool Enabled { get; init; } = true;

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
