namespace Domain.Game.Rules;

/// <summary>The combined effect of every perk a snake has.</summary>
public readonly record struct PerkEffects
{
    public static PerkEffects None { get; } = new();

    public int ExtraShots { get; init; }
    public double BoostSpeedBonus { get; init; }
    /// <summary>Share of the shot cooldown left, 1 without perks.</summary>
    public double ReloadFactor { get; init; } = 1.0;
    /// <summary>How many times longer one mass of boost lasts, 1 without perks.</summary>
    public double BoostLengthFactor { get; init; } = 1.0;
    /// <summary>Shots fired after the first one, each time you shoot.</summary>
    public int BurstExtraShots { get; init; }
    public double ShotSpeedFactor { get; init; } = 1.0;
    public bool AutoFire { get; init; }
    public bool Piercing { get; init; }
    /// <summary>Share of shot damage taken, 1 without perks.</summary>
    public double DamageFactor { get; init; } = 1.0;
    public double RegenPerSecond { get; init; }
    public double MagnetRadius { get; init; }

    public PerkEffects() { }
}

/// <summary>Which perks a snake has for the foods it ate, and what they do.</summary>
public sealed class PerkRules
{
    private const double MinReloadFactor = 0.2;
    private const double MinDamageFactor = 0.2;

    /// <summary>Everything the rules read, kept together so a change of options swaps it all at once.</summary>
    private sealed record Settings(
        PerkOptions Options, bool Enabled, double SpreadRadians, IReadOnlyList<PerkDefinition> Perks);

    private volatile Settings _settings;

    public PerkRules(PerkOptions options)
    {
        _settings = Build(options);
    }

    /// <summary>
    /// Switches to other options while the game runs. Options that do not hold up are refused and the old ones stay.
    /// </summary>
    public void Reconfigure(PerkOptions options) => _settings = Build(options);

    /// <summary>The options in use.</summary>
    public PerkOptions Options => _settings.Options;

    /// <summary>Time between the shots of a burst.</summary>
    public int BurstIntervalMilliseconds => _settings.Options.BurstIntervalMilliseconds;

    private static Settings Build(PerkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShotSpreadDegrees is <= 0 or >= 90)
            throw new ArgumentOutOfRangeException(nameof(options), options.ShotSpreadDegrees, "Shot spread must be in (0, 90).");
        if (options.BurstIntervalMilliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(options), options.BurstIntervalMilliseconds, "Burst interval must be at least 1 ms.");
        var perks = Validated(options.Perks.Count == 0 ? PerkOptions.BuiltIn : options.Perks)
            .OrderBy(perk => perk.FoodsRequired)
            .ToArray();
        return new Settings(options, options.Enabled, options.ShotSpreadDegrees * Math.PI / 180, perks);
    }

    /// <summary>Ids of the perks earned with <paramref name="foodsEaten"/> foods, in the order they unlock.</summary>
    public IReadOnlyList<string> UnlockedIds(int foodsEaten)
        => Unlocked(foodsEaten).Select(perk => perk.Id).ToArray();

    public PerkEffects EffectsFor(int foodsEaten)
    {
        var unlocked = Unlocked(foodsEaten).ToArray();
        if (unlocked.Length == 0) return PerkEffects.None;
        return new PerkEffects
        {
            ExtraShots = (int)Math.Round(Sum(unlocked, PerkEffect.ExtraShots)),
            BoostSpeedBonus = Sum(unlocked, PerkEffect.BoostSpeed),
            ReloadFactor = Math.Max(MinReloadFactor, Product(unlocked, PerkEffect.FasterReload)),
            BoostLengthFactor = 1.0 + Sum(unlocked, PerkEffect.EfficientBoost),
            BurstExtraShots = (int)Math.Round(Sum(unlocked, PerkEffect.BurstShot)),
            ShotSpeedFactor = 1.0 + Sum(unlocked, PerkEffect.ShotSpeed),
            AutoFire = Has(unlocked, PerkEffect.AutoFire),
            Piercing = Has(unlocked, PerkEffect.PiercingShot),
            DamageFactor = Math.Max(MinDamageFactor, Product(unlocked, PerkEffect.Armor)),
            RegenPerSecond = Sum(unlocked, PerkEffect.Regeneration),
            MagnetRadius = Sum(unlocked, PerkEffect.Magnet)
        };
    }

    /// <summary>Angles (radians) to add to the aim for each shot of a volley of <paramref name="shots"/>, centred on the aim.</summary>
    public IEnumerable<double> ShotOffsets(int shots)
    {
        if (shots < 1) throw new ArgumentOutOfRangeException(nameof(shots), shots, "Must be at least 1.");
        var spread = _settings.SpreadRadians;
        return Enumerable.Range(0, shots).Select(index => (index - (shots - 1) / 2.0) * spread);
    }

    private IEnumerable<PerkDefinition> Unlocked(int foodsEaten)
    {
        var settings = _settings;
        return settings.Enabled ? settings.Perks.Where(perk => perk.FoodsRequired <= foodsEaten) : [];
    }

    private static bool Has(IEnumerable<PerkDefinition> perks, PerkEffect effect)
        => perks.Any(perk => perk.Effect == effect);

    private static double Sum(IEnumerable<PerkDefinition> perks, PerkEffect effect)
        => perks.Where(perk => perk.Effect == effect).Sum(perk => perk.Amount);

    private static double Product(IEnumerable<PerkDefinition> perks, PerkEffect effect)
        => perks.Where(perk => perk.Effect == effect).Aggregate(1.0, (factor, perk) => factor * (1 - perk.Amount));

    private static IReadOnlyList<PerkDefinition> Validated(IReadOnlyList<PerkDefinition> perks)
    {
        EnsureUniqueIds(perks);
        foreach (var perk in perks) EnsureValid(perk);
        return perks;
    }

    private static void EnsureUniqueIds(IEnumerable<PerkDefinition> perks)
    {
        var repeated = perks.GroupBy(perk => perk.Id).FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null) throw new ArgumentException($"Perk id '{repeated.Key}' is used twice.", nameof(perks));
    }

    private static void EnsureValid(PerkDefinition perk)
    {
        if (string.IsNullOrWhiteSpace(perk.Id))
            throw new ArgumentException("A perk needs an id.", nameof(perk));
        if (perk.FoodsRequired < 1)
            throw new ArgumentOutOfRangeException(nameof(perk), perk.FoodsRequired, $"Perk '{perk.Id}' needs at least 1 food.");
        if (perk.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(perk), perk.Amount, $"Perk '{perk.Id}' needs an amount above zero.");
        var isRatio = perk.Effect is PerkEffect.FasterReload or PerkEffect.EfficientBoost or PerkEffect.Armor;
        if (isRatio && perk.Amount >= 1)
            throw new ArgumentOutOfRangeException(nameof(perk), perk.Amount, $"Perk '{perk.Id}' needs an amount below 1.");
    }
}
