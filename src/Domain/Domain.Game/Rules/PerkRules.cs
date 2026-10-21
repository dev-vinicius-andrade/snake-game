using Domain.Game.Scripting;
using Library.Commons.Game.Domain.Entities;
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
    /// <summary>True while nothing can hurt the snake: shots and other snakes pass it by.</summary>
    public bool Invulnerable { get; init; }
    /// <summary>True while shooting costs no mass.</summary>
    public bool FreeShots { get; init; }
    /// <summary>Share of the angle between the shots of a volley that is left, 1 without precision.</summary>
    public double SpreadFactor { get; init; } = 1.0;
    /// <summary>Times the snake is saved from dying.</summary>
    public int ExtraLives { get; init; }
    /// <summary>Share of the mass of a snake it kills that the snake takes at once.</summary>
    public double PlunderShare { get; init; }
    public double LightningResistance { get; init; }
    public double MeteorResistance { get; init; }
    public double MineResistance { get; init; }
    public double ZoneResistance { get; init; }
    /// <summary>Added to the speed factor while a dash is on. Set by the game from the abilities in use, never by the perks themselves.</summary>
    public double AbilitySpeedBonus { get; init; }

    public PerkEffects() { }

    /// <summary>Share (0..1) of the mass an event would take that the snake is spared: <c>lightning</c>, <c>meteors</c>, <c>mines</c> or <c>zone</c>.</summary>
    public double ResistanceTo(string kind) => kind switch
    {
        PerkRules.Lightning => LightningResistance,
        PerkRules.Meteors => MeteorResistance,
        PerkRules.Mines => MineResistance,
        PerkRules.Zone => ZoneResistance,
        _ => 0
    };
}

/// <summary>A cooldown ability a snake has: what it does, how strongly, how long it lasts and how long it takes to recharge.</summary>
public sealed record AbilitySpec(string PerkId, PerkEffect Effect, double Amount, int CooldownMilliseconds, int DurationMilliseconds);

/// <summary>Which perks a snake has for the foods it ate, and what they do.</summary>
public sealed class PerkRules
{
    public const int MaxDescriptionLength = 200;

    private static readonly System.Text.RegularExpressions.Regex IconName = new(@"^(mdi-[a-z0-9]+(-[a-z0-9]+)*|game-icons:[a-z0-9]+(-[a-z0-9]+)*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public const string Lightning = "lightning";
    public const string Meteors = "meteors";
    public const string Mines = "mines";
    public const string Zone = "zone";

    /// <summary>The world events a resistance can be limited to.</summary>
    public static IReadOnlyList<string> EventKinds { get; } = [Lightning, Meteors, Mines, Zone];

    /// <summary>The tightest a volley can be made: precision never folds the fan into one line.</summary>
    private const double MinSpreadFactor = 0.1;

    private const int MaxExtraLives = 5;

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

    /// <summary>How many foods killing another snake counts for toward perks and their tiers.</summary>
    public int KillFoodsCredit => _settings.Options.KillFoodsCredit;

    /// <summary>Share of its mass a snake keeps when an extra life saves it.</summary>
    public double ExtraLifeKeepShare => _settings.Options.ExtraLifeKeepShare;

    /// <summary>Seconds a snake that an extra life saved cannot be hurt.</summary>
    public int ExtraLifeProtectSeconds => _settings.Options.ExtraLifeProtectSeconds;

    private static Settings Build(PerkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShotSpreadDegrees is <= 0 or >= 90)
            throw new ArgumentOutOfRangeException(nameof(options), options.ShotSpreadDegrees, "Shot spread must be in (0, 90).");
        if (options.KillFoodsCredit is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options), options.KillFoodsCredit, "The credit for a kill must be 0 to 1000 foods.");
        if (options.ExtraLifeKeepShare is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options), options.ExtraLifeKeepShare, "The share of mass an extra life keeps must be 0 to 1.");
        if (options.ExtraLifeProtectSeconds is < 0 or > 60)
            throw new ArgumentOutOfRangeException(nameof(options), options.ExtraLifeProtectSeconds, "The protection after an extra life must be 0 to 60 seconds.");
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
    public PerkEffects EffectsFor(int foodsEaten, IRoom? room, ScriptContext context, IReadOnlyCollection<string>? granted = null, IReadOnlyDictionary<string, int>? surges = null)
    {
        var effects = EffectsFor(foodsEaten, room, granted, surges);
        foreach (var (perk, script) in ScriptedPerks(foodsEaten, room, granted))
            if (script.Modify(context.Snake, context.Room, context.SnakeStates?.Of(perk.Id)) is { } added)
                effects = Merge(effects, added);
        return effects;
    }

    /// <summary>What the scripts of this snake's perks give for eating, added up.</summary>
    public ScriptRewards RewardsForEating(int foodsEaten, IRoom? room, ScriptContext context, ScriptFood food, IReadOnlyCollection<string>? granted = null)
    {
        var rewards = new ScriptRewards(0, 0);
        foreach (var (perk, script) in ScriptedPerks(foodsEaten, room, granted))
            if (script.OnEat(context.Snake, food, context.Room, context.SnakeStates?.Of(perk.Id)) is { } given)
                rewards = Add(rewards, given);
        return rewards;
    }

    /// <summary>What the scripts of this snake's perks give for killing another snake, added up.</summary>
    public ScriptRewards RewardsForKilling(int foodsEaten, IRoom? room, ScriptContext context, ScriptVictim victim, IReadOnlyCollection<string>? granted = null)
    {
        var rewards = new ScriptRewards(0, 0);
        foreach (var (perk, script) in ScriptedPerks(foodsEaten, room, granted))
            if (script.OnKill(context.Snake, victim, context.Room, context.SnakeStates?.Of(perk.Id)) is { } given)
                rewards = Add(rewards, given);
        return rewards;
    }

    private static ScriptRewards Add(ScriptRewards total, ScriptRewards given) => new(total.Mass + given.Mass, total.Heal + given.Heal);

    /// <summary>
    /// Runs the <c>onTick</c> of every script that works in the room, whoever is in it, and gives back the kinds of prize they asked
    /// to drop. Meant to be called about once a second.
    /// </summary>
    public IReadOnlyList<string> TickRoom(IRoom room, ScriptRoom view, ScriptStates states)
    {
        var settings = _settings;
        if (!settings.Enabled || room.GameplayOf().PerksOff) return [];
        return settings.Perks
            .Where(perk => perk.HasScript && perk.AppliesTo(room) && settings.Scripts.TryGetValue(perk.Id, out var script) && script.HasOnTick)
            .SelectMany(perk => settings.Scripts[perk.Id].OnTick(view, states.Of(perk.Id))?.DropPrizes ?? [])
            .Distinct()
            .ToArray();
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
        Invulnerable = effects.Invulnerable || added.Invulnerable == true,
        FreeShots = effects.FreeShots || added.FreeShots == true,
    };

    /// <summary>Ids of the perks earned with <paramref name="foodsEaten"/> foods, in the order they unlock.</summary>
    public IReadOnlyList<string> UnlockedIds(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null)
        => Unlocked(foodsEaten, room, granted).Select(perk => perk.Id).ToArray();

    /// <summary>The tier each perk a snake has is at, 1 for the first, in the order of <see cref="UnlockedIds"/>.</summary>
    public IReadOnlyList<int> TiersFor(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null, IReadOnlyDictionary<string, int>? surges = null)
        => Unlocked(foodsEaten, room, granted).Select(perk => TierOf(perk, foodsEaten, SurgeOf(surges, perk)).Number).ToArray();

    private static int SurgeOf(IReadOnlyDictionary<string, int>? surges, PerkDefinition perk)
        => surges is not null && surges.TryGetValue(perk.Id, out var surge) ? Math.Max(0, surge) : 0;

    /// <summary>
    /// The tier a perk is at, and what it multiplies its amounts by. The rule for surges: the tier is the one the foods give (the highest
    /// eatable tier reached; the first for a perk that was given with few foods; reward-only tiers are never reached by foods) plus the tiers a reward added, but never past the last tier of the perk. So a surge is not lost
    /// as foods go up, and a perk at its last tier is not lifted further.
    /// </summary>
    private static (int Number, double Multiplier) TierOf(PerkDefinition perk, int foodsEaten, int surge = 0)
    {
        var tiers = perk.TiersInOrder();
        var reached = Math.Max(1, tiers.Count(tier => !tier.RewardOnly && tier.FoodsRequired <= foodsEaten));
        var number = Math.Min(tiers.Count, reached + Math.Max(0, surge));
        return (number, tiers[number - 1].Multiplier);
    }

    /// <summary>The perks a snake has that a tier surge can still lift: those with a tier above the one they are at.</summary>
    public IReadOnlyList<string> SurgeCandidates(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null, IReadOnlyDictionary<string, int>? surges = null)
        => Unlocked(foodsEaten, room, granted)
            .Where(perk => TierOf(perk, foodsEaten, SurgeOf(surges, perk)).Number < perk.TiersInOrder().Count)
            .Select(perk => perk.Id).ToArray();

    /// <summary>The most tiers the perk has, 0 for a perk that does not exist.</summary>
    public int TierCountOf(string perkId) => _settings.Perks.FirstOrDefault(perk => perk.Id == perkId)?.TiersInOrder().Count ?? 0;

    /// <summary>Perks the snake does not have that work in this room and are on: what a random perk reward can give.</summary>
    public IReadOnlyList<string> UnownedPerkIds(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null)
    {
        var settings = _settings;
        if (!settings.Enabled || room.GameplayOf().PerksOff) return [];
        var owned = UnlockedIds(foodsEaten, room, granted).ToHashSet();
        return settings.Perks.Where(perk => perk.AppliesTo(room) && !owned.Contains(perk.Id)).Select(perk => perk.Id).ToArray();
    }

    /// <summary>The cooldown abilities of the perks a snake has, each at the strength its tier gives.</summary>
    public IReadOnlyList<AbilitySpec> AbilitiesFor(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null, IReadOnlyDictionary<string, int>? surges = null)
        => Unlocked(foodsEaten, room, granted)
            .SelectMany(perk => Scaled(perk, TierOf(perk, foodsEaten, SurgeOf(surges, perk)).Multiplier)
                .Where(entry => entry.Effect.IsAbility())
                .Select(entry => new AbilitySpec(perk.Id, entry.Effect, entry.Amount, perk.CooldownSeconds * 1000, perk.DurationSeconds * 1000)))
            .ToArray();

    /// <summary>What the perks a snake has do, in the room it plays in.</summary>
    public PerkEffects EffectsFor(int foodsEaten, IRoom? room = null, IReadOnlyCollection<string>? granted = null, IReadOnlyDictionary<string, int>? surges = null)
    {
        var unlocked = Entries(Unlocked(foodsEaten, room, granted), foodsEaten, surges);
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
            MagnetRadius = Sum(unlocked, PerkEffect.Magnet),
            SpreadFactor = Math.Max(MinSpreadFactor, Product(unlocked, PerkEffect.Precision)),
            ExtraLives = Math.Min(MaxExtraLives, (int)Math.Round(Sum(unlocked, PerkEffect.ExtraLife))),
            PlunderShare = Sum(unlocked, PerkEffect.Plunder),
            LightningResistance = Resistance(unlocked, Lightning),
            MeteorResistance = Resistance(unlocked, Meteors),
            MineResistance = Resistance(unlocked, Mines),
            ZoneResistance = Resistance(unlocked, Zone)
        };
    }

    /// <summary>What is spared of an event: shares of what is left, like armor, from the entries that are for all events or for this one.</summary>
    private static double Resistance(IEnumerable<PerkEffectEntry> entries, string kind)
        => 1 - entries
            .Where(entry => entry.Effect == PerkEffect.EventResistance && (string.IsNullOrWhiteSpace(entry.Target) || entry.Target == kind))
            .Aggregate(1.0, (left, entry) => left * (1 - entry.Amount));

    /// <summary>Angles (radians) to add to the aim for each shot of a volley of <paramref name="shots"/>, centred on the aim.</summary>
    public IEnumerable<double> ShotOffsets(int shots, double spreadFactor = 1)
    {
        if (shots < 1) throw new ArgumentOutOfRangeException(nameof(shots), shots, "Must be at least 1.");
        var spread = _settings.SpreadRadians * spreadFactor;
        return Enumerable.Range(0, shots).Select(index => (index - (shots - 1) / 2.0) * spread);
    }

    /// <summary>The perks a snake has: those its foods earned and those it was given, that work here.</summary>
    private IEnumerable<PerkDefinition> Unlocked(int foodsEaten, IRoom? room, IReadOnlyCollection<string>? granted = null)
    {
        var settings = _settings;
        return settings.Enabled && !room.GameplayOf().PerksOff
            ? settings.Perks.Where(perk => (perk.FoodsRequired <= foodsEaten || granted?.Contains(perk.Id) == true) && perk.AppliesTo(room))
            : [];
    }

    /// <summary>Most a ratio effect (armor, reload, deep lungs) can be after a tier scaled it: a perk can never make a snake immune.</summary>
    private const double MaxScaledRatio = 0.95;

    /// <summary>The effects of the perks, each scaled by the tier its perk is at. Switches have no strength to scale.</summary>
    private static PerkEffectEntry[] Entries(IEnumerable<PerkDefinition> perks, int foodsEaten, IReadOnlyDictionary<string, int>? surges = null)
        => perks.SelectMany(perk => Scaled(perk, TierOf(perk, foodsEaten, SurgeOf(surges, perk)).Multiplier)).ToArray();

    private static IEnumerable<PerkEffectEntry> Scaled(PerkDefinition perk, double multiplier)
    {
        if (multiplier == 1) return perk.AllEffects();
        return perk.AllEffects().Select(entry => entry.Effect switch
        {
            var effect when effect.IsSwitch() => entry,
            var effect when effect.IsRatio() => entry with { Amount = Math.Min(MaxScaledRatio, entry.Amount * multiplier) },
            PerkEffect.ExtraLife => entry with { Amount = Math.Min(MaxExtraLives, Math.Round(entry.Amount * multiplier)) },
            _ => entry with { Amount = entry.Amount * multiplier }
        });
    }

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
        EnsureValidAbilities(perk, effects);
        EnsureValidTiers(perk);
        if (perk.Rooms.Concat(perk.ExcludedRooms).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Perk '{perk.Id}' has a blank room.", nameof(perk));
        if (!string.IsNullOrWhiteSpace(perk.Icon) && (perk.Icon.Length > 40 || !IconName.IsMatch(perk.Icon)))
            throw new ArgumentException($"Perk '{perk.Id}' icon must be a game icon such as game-icons:magnet, or a Material Design Icons name such as mdi-shield.", nameof(perk));
        if (perk.Description is { Length: > MaxDescriptionLength })
            throw new ArgumentException($"Perk '{perk.Id}' description is too long: at most {MaxDescriptionLength} characters.", nameof(perk));
    }

    private static void EnsureValidTiers(PerkDefinition perk)
    {
        if (perk.Tiers.Count + 1 > PerkDefinition.MaxTiers)
            throw new ArgumentException($"Perk '{perk.Id}' has too many tiers: at most {PerkDefinition.MaxTiers}, the first included.", nameof(perk));
        foreach (var tier in perk.Tiers)
        {
            if (tier.FoodsRequired <= perk.FoodsRequired)
                throw new ArgumentOutOfRangeException(nameof(perk), tier.FoodsRequired, $"Perk '{perk.Id}': a tier must ask for more than the {perk.FoodsRequired} foods of the first.");
            if (!(tier.Multiplier > 0 && tier.Multiplier <= 10))
                throw new ArgumentOutOfRangeException(nameof(perk), tier.Multiplier, $"Perk '{perk.Id}': a tier must multiply by more than 0 and at most 10.");
        }
        var ordered = perk.Tiers.OrderBy(tier => tier.FoodsRequired).ToList();
        var firstRewardOnly = ordered.FindIndex(tier => tier.RewardOnly);
        if (firstRewardOnly >= 0 && ordered.Skip(firstRewardOnly).Any(tier => !tier.RewardOnly))
            throw new ArgumentException($"Perk '{perk.Id}': a reward-only tier must come after every tier that foods can reach.", nameof(perk));
        if (perk.Tiers.Zip(perk.Tiers.Skip(1), (before, after) => after.FoodsRequired <= before.FoodsRequired).Any(notIncreasing => notIncreasing))
            throw new ArgumentException($"Perk '{perk.Id}': each tier must ask for more foods than the one before.", nameof(perk));
    }

    private static void EnsureValid(PerkDefinition perk, PerkEffectEntry entry)
    {
        if (entry.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(perk), entry.Amount, $"Perk '{perk.Id}' needs an amount above zero.");
        if (entry.Effect.IsRatio() && entry.Amount >= 1)
            throw new ArgumentOutOfRangeException(nameof(perk), entry.Amount, $"Perk '{perk.Id}' needs an amount below 1.");
        if (entry.Effect == PerkEffect.ExtraLife && entry.Amount > MaxExtraLives)
            throw new ArgumentOutOfRangeException(nameof(perk), entry.Amount, $"Perk '{perk.Id}': an extra life perk gives at most {MaxExtraLives} lives.");
        if (!string.IsNullOrWhiteSpace(entry.Target) && (entry.Effect != PerkEffect.EventResistance || !EventKinds.Contains(entry.Target)))
            throw new ArgumentException($"Perk '{perk.Id}': a target is only for event resistance, and is one of {string.Join(", ", EventKinds)}.", nameof(perk));
    }

    private static void EnsureValidAbilities(PerkDefinition perk, IReadOnlyList<PerkEffectEntry> effects)
    {
        if (perk.CooldownSeconds is < 0 or > 3600 || perk.DurationSeconds is < 0 or > 600)
            throw new ArgumentOutOfRangeException(nameof(perk), perk.CooldownSeconds, $"Perk '{perk.Id}': the cooldown is 0 to 3600 seconds and the duration 0 to 600.");
        var abilities = effects.Where(entry => entry.Effect.IsAbility()).ToArray();
        if (abilities.Length == 0) return;
        if (perk.CooldownSeconds < 1)
            throw new ArgumentException($"Perk '{perk.Id}' has a cooldown ability and needs a cooldown of at least 1 second.", nameof(perk));
        if (perk.DurationSeconds < 1 && abilities.Any(entry => entry.Effect != PerkEffect.HealPulse))
            throw new ArgumentException($"Perk '{perk.Id}': a shield burst, dash or vacuum burst needs a duration of at least 1 second.", nameof(perk));
    }
}
