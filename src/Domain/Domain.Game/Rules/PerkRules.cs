using Domain.Game.Scripting;
using Library.Commons.Game.Domain.Interfaces.Entities;

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
        PerkOptions Options, bool Enabled, double SpreadRadians, IReadOnlyList<PerkDefinition> Perks,
        IReadOnlyDictionary<string, PerkScript> Scripts);

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
        return new Settings(options, options.Enabled, options.ShotSpreadDegrees * Math.PI / 180, perks, CompileScripts(perks));
    }

    /// <summary>Reads and checks every script. A script that cannot be used refuses the whole config, naming its perk.</summary>
    private static Dictionary<string, PerkScript> CompileScripts(IEnumerable<PerkDefinition> perks)
    {
        var scripts = new Dictionary<string, PerkScript>();
        foreach (var perk in perks.Where(perk => perk.HasScript))
        {
            try
            {
                var script = PerkScript.Compile(perk.Script!);
                script.Check();
                scripts[perk.Id] = script;
            }
            catch (ScriptException exception)
            {
                throw new ArgumentException($"Perk '{perk.Id}' script: {exception.Message}", nameof(perks), exception);
            }
        }
        return scripts;
    }

    /// <summary>The compiled script of a perk, or null.</summary>
    public PerkScript? ScriptOf(string perkId) => _settings.Scripts.GetValueOrDefault(perkId);

    /// <summary>True when a script of a perk the snake has works in this room, so its effects must be asked for with a context.</summary>
    public bool HasScripts(int foodsEaten, IRoom? room, IReadOnlyCollection<string>? granted = null) => ScriptedPerks(foodsEaten, room, granted).Any();

    private IEnumerable<(PerkDefinition Perk, PerkScript Script)> ScriptedPerks(int foodsEaten, IRoom? room, IReadOnlyCollection<string>? granted)
    {
        var settings = _settings;
        return Unlocked(foodsEaten, room, granted)
            .Where(perk => perk.HasScript && settings.Scripts.ContainsKey(perk.Id))
            .Select(perk => (perk, settings.Scripts[perk.Id]));
    }

    /// <summary>What the perks do for this snake, with what the scripts of its perks add in this room.</summary>
    public PerkEffects EffectsFor(int foodsEaten, IRoom? room, ScriptContext context, IReadOnlyCollection<string>? granted = null)
    {
        var effects = EffectsFor(foodsEaten, room, granted);
        foreach (var (_, script) in ScriptedPerks(foodsEaten, room, granted))
            if (script.Modify(context.Snake, context.Room) is { } added)
                effects = Merge(effects, added);
        return effects;
    }

    /// <summary>What the scripts of this snake's perks give for eating, added up.</summary>
    public ScriptRewards RewardsForEating(int foodsEaten, IRoom? room, ScriptContext context, ScriptFood food, IReadOnlyCollection<string>? granted = null)
    {
        var rewards = new ScriptRewards(0, 0);
        foreach (var (_, script) in ScriptedPerks(foodsEaten, room, granted))
            if (script.OnEat(context.Snake, food, context.Room) is { } given)
                rewards = new ScriptRewards(rewards.Mass + given.Mass, rewards.Heal + given.Heal);
        return rewards;
    }

    /// <summary>Adds what a script returned: sums add, factors multiply (kept in range), switches are on when either says so.</summary>
    private static PerkEffects Merge(PerkEffects effects, ScriptEffects added) => effects with
    {
        ExtraShots = effects.ExtraShots + (added.ExtraShots ?? 0),
        BurstExtraShots = effects.BurstExtraShots + (added.BurstExtraShots ?? 0),
        BoostSpeedBonus = effects.BoostSpeedBonus + (added.BoostSpeedBonus ?? 0),
        RegenPerSecond = effects.RegenPerSecond + (added.RegenPerSecond ?? 0),
        MagnetRadius = effects.MagnetRadius + (added.MagnetRadius ?? 0),
        BoostLengthFactor = effects.BoostLengthFactor * (added.BoostLengthFactor ?? 1),
        ShotSpeedFactor = effects.ShotSpeedFactor * (added.ShotSpeedFactor ?? 1),
        ReloadFactor = Math.Max(MinReloadFactor, effects.ReloadFactor * (added.ReloadFactor ?? 1)),
        DamageFactor = Math.Max(MinDamageFactor, effects.DamageFactor * (added.DamageFactor ?? 1)),
        AutoFire = effects.AutoFire || added.AutoFire == true,
        Piercing = effects.Piercing || added.Piercing == true,
    };

    /// <summary>Ids of the perks earned with <paramref name="foodsEaten"/> foods, in the order they unlock.</summary>
    public IReadOnlyList<string> UnlockedIds(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null)
        => Unlocked(foodsEaten, room, granted).Select(perk => perk.Id).ToArray();

    /// <summary>What the perks a snake has do, in the room it plays in.</summary>
    public PerkEffects EffectsFor(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null)
    {
        var unlocked = Entries(Unlocked(foodsEaten, room, granted));
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

    /// <summary>The perks a snake has: those its foods earned and those it was given, that work here.</summary>
    private IEnumerable<PerkDefinition> Unlocked(int foodsEaten, IRoom? room, IReadOnlyCollection<string>? granted = null)
    {
        var settings = _settings;
        return settings.Enabled
            ? settings.Perks.Where(perk => (perk.FoodsRequired <= foodsEaten || granted?.Contains(perk.Id) == true) && perk.AppliesTo(room))
            : [];
    }

    private static PerkEffectEntry[] Entries(IEnumerable<PerkDefinition> perks)
        => perks.SelectMany(perk => perk.AllEffects()).ToArray();

    private static bool Has(IEnumerable<PerkEffectEntry> entries, PerkEffect effect)
        => entries.Any(entry => entry.Effect == effect);

    private static double Sum(IEnumerable<PerkEffectEntry> entries, PerkEffect effect)
        => entries.Where(entry => entry.Effect == effect).Sum(entry => entry.Amount);

    private static double Product(IEnumerable<PerkEffectEntry> entries, PerkEffect effect)
        => entries.Where(entry => entry.Effect == effect).Aggregate(1.0, (factor, entry) => factor * (1 - entry.Amount));

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
        var effects = perk.AllEffects();
        if (effects.Count == 0 && !perk.HasScript)
            throw new ArgumentException($"Perk '{perk.Id}' needs at least one effect, or a script.", nameof(perk));
        foreach (var entry in effects) EnsureValid(perk, entry);
        if (perk.Rooms.Concat(perk.ExcludedRooms).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Perk '{perk.Id}' has a blank room.", nameof(perk));
    }

    private static void EnsureValid(PerkDefinition perk, PerkEffectEntry entry)
    {
        if (entry.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(perk), entry.Amount, $"Perk '{perk.Id}' needs an amount above zero.");
        var isRatio = entry.Effect is PerkEffect.FasterReload or PerkEffect.EfficientBoost or PerkEffect.Armor;
        if (isRatio && entry.Amount >= 1)
            throw new ArgumentOutOfRangeException(nameof(perk), entry.Amount, $"Perk '{perk.Id}' needs an amount below 1.");
    }
}
