namespace Domain.Game.Rules;

/// <summary>The combined effect of every perk a snake has.</summary>
public readonly record struct PerkEffects(int ExtraShots, double BoostSpeedBonus, double ReloadFactor, double BoostLengthFactor)
{
    public static PerkEffects None { get; } = new(0, 0, 1.0, 1.0);
}

/// <summary>Which perks a snake has for the foods it ate, and what they do.</summary>
public sealed class PerkRules
{
    private const double MinReloadFactor = 0.2;

    private readonly bool _enabled;
    private readonly double _spreadRadians;
    private readonly IReadOnlyList<PerkDefinition> _perks;

    public PerkRules(PerkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShotSpreadDegrees is <= 0 or >= 90)
            throw new ArgumentOutOfRangeException(nameof(options), options.ShotSpreadDegrees, "Shot spread must be in (0, 90).");
        _enabled = options.Enabled;
        _spreadRadians = options.ShotSpreadDegrees * Math.PI / 180;
        _perks = Validated(options.Perks.Count == 0 ? PerkOptions.BuiltIn : options.Perks)
            .OrderBy(perk => perk.FoodsRequired)
            .ToArray();
    }

    /// <summary>Ids of the perks earned with <paramref name="foodsEaten"/> foods, in the order they unlock.</summary>
    public IReadOnlyList<string> UnlockedIds(int foodsEaten)
        => Unlocked(foodsEaten).Select(perk => perk.Id).ToArray();

    public PerkEffects EffectsFor(int foodsEaten)
    {
        var unlocked = Unlocked(foodsEaten).ToArray();
        if (unlocked.Length == 0) return PerkEffects.None;
        return new PerkEffects(
            (int)Math.Round(Sum(unlocked, PerkEffect.ExtraShots)),
            Sum(unlocked, PerkEffect.BoostSpeed),
            Math.Max(MinReloadFactor, Product(unlocked, PerkEffect.FasterReload)),
            1.0 + Sum(unlocked, PerkEffect.EfficientBoost));
    }

    /// <summary>Angles (radians) to add to the aim for each shot of a volley of <paramref name="shots"/>, centred on the aim.</summary>
    public IEnumerable<double> ShotOffsets(int shots)
    {
        if (shots < 1) throw new ArgumentOutOfRangeException(nameof(shots), shots, "Must be at least 1.");
        return Enumerable.Range(0, shots).Select(index => (index - (shots - 1) / 2.0) * _spreadRadians);
    }

    private IEnumerable<PerkDefinition> Unlocked(int foodsEaten)
        => _enabled ? _perks.Where(perk => perk.FoodsRequired <= foodsEaten) : [];

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
        var isRatio = perk.Effect is PerkEffect.FasterReload or PerkEffect.EfficientBoost;
        if (isRatio && perk.Amount >= 1)
            throw new ArgumentOutOfRangeException(nameof(perk), perk.Amount, $"Perk '{perk.Id}' needs an amount below 1.");
    }
}
