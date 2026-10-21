using Library.Commons.Game.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Game.Rules;

/// <summary>
/// Everything an administrator can tune while the game runs: the perks, the prize, the room lifetimes and the world events. The manager keeps it and the game
/// servers fetch it, so both sides read and write it the same way (<see cref="Json"/>).
/// </summary>
public sealed record GameplayConfig
{
    public PerkOptions Perks { get; init; } = new();
    public PrizeOptions Prize { get; init; } = new();
    public RoomLifetimeOptions Rooms { get; init; } = new();
    public WorldEventOptions WorldEvents { get; init; } = new();

    /// <summary>
    /// The rewards, each with a name. A prize, the turret, the bounty and the maze pick from here by id (<c>RewardIds</c>) and give every one
    /// they picked. A config written before the catalog carried its rewards inside each of them; <see cref="Normalized"/> moves those here.
    /// </summary>
    public List<RewardDefinition> Rewards { get; init; } = [];

    /// <summary>How the config is written and read: camel case names, and effects by name.</summary>
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The built-in perks listed out, so an editor shows them, and the default prize.</summary>
    public static GameplayConfig Defaults => (new GameplayConfig() with { WorldEvents = new WorldEventOptions().WithStormDefaults() }).WithBuiltInPerks();

    /// <summary>An empty perk list means the built-in set; this spells it out so it can be edited. Perks come out normalized.</summary>
    public GameplayConfig WithBuiltInPerks()
        => (Perks.Perks.Count > 0 ? this : this with { Perks = Perks with { Perks = PerkOptions.BuiltIn.ToList() } }).Normalized();

    /// <summary>Every perk with its effects in the list, the single old effect folded into it. Nothing else changes.</summary>
    public GameplayConfig Normalized() => (this with
    {
        Perks = Perks with
        {
            Perks = Perks.Perks.Select(perk => perk with { Effects = perk.AllEffects().ToList(), Effect = null, Amount = 0 }).ToList()
        },
        Prize = Prize with { Prizes = Prize.Definitions().ToList() }
    }).WithRewardsResolved();

    /// <summary>
    /// Settles the rewards: a reward written inside a prize, the turret, the bounty or the maze (and not picked from the catalog) becomes
    /// a catalog entry that it picks, so nothing is lost; and what each of them gives is worked out from what it picked.
    /// </summary>
    public GameplayConfig WithRewardsResolved()
    {
        var catalog = Rewards.ToList();
        var events = WorldEvents;

        (List<string> Ids, IReadOnlyList<RewardDefinition> Picked, PerkReward Reward) Settle(string key, string label, List<string> ids, PerkReward embedded)
        {
            if (ids.Count == 0 && !embedded.IsEmpty) ids = [AddToCatalog(catalog, $"{key}-reward", $"{label} reward", embedded)];
            var picked = ids.Select(id => catalog.FirstOrDefault(reward => SameId(reward.Id, id))).OfType<RewardDefinition>().ToList();
            return (ids, picked, ids.Count == 0 ? embedded : PerkReward.Combine(picked.Select(reward => reward.Reward)));
        }

        var prizes = Prize.Definitions().Select(prize =>
        {
            var (ids, picked, reward) = Settle($"prize-{prize.Id}", string.IsNullOrWhiteSpace(prize.Name) ? prize.Id : prize.Name, prize.RewardIds, prize.Reward);
            return prize with { RewardIds = ids, PickedRewards = picked, Reward = reward };
        }).ToList();
        var turret = Settle("turret", "Turret", events.Turret.RewardIds, events.Turret.Reward);
        var bounty = Settle("bounty", "Bounty", events.Bounty.RewardIds, events.Bounty.Reward);
        var maze = Settle("maze", "Maze prize", events.Maze.RewardIds, events.Maze.PrizeReward);
        return this with
        {
            Rewards = catalog,
            Prize = Prize with { Prizes = prizes },
            WorldEvents = events with
            {
                Turret = events.Turret with { RewardIds = turret.Ids, PickedRewards = turret.Picked, Reward = turret.Reward },
                Bounty = events.Bounty with { RewardIds = bounty.Ids, PickedRewards = bounty.Picked, Reward = bounty.Reward },
                Maze = events.Maze with { RewardIds = maze.Ids, PickedRewards = maze.Picked, PrizeReward = maze.Reward },
            }
        };
    }

    private static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string AddToCatalog(List<RewardDefinition> catalog, string id, string name, PerkReward reward)
    {
        id = id.Length > MaxRewardIdLength ? id[..MaxRewardIdLength] : id;
        var unique = id;
        for (var suffix = 2; catalog.Any(existing => SameId(existing.Id, unique)); suffix++)
        {
            var tail = $"-{suffix}";
            unique = id[..Math.Min(id.Length, MaxRewardIdLength - tail.Length)] + tail;
        }
        catalog.Add(new RewardDefinition { Id = unique, Name = name.Length > 64 ? name[..64] : name, Reward = reward });
        return unique;
    }

    /// <summary>Throws when the perks or the prize could not run.</summary>
    public void EnsureValid()
    {
        _ = new PerkRules(Perks);
        _ = new PrizeRules(Prize);
        EnsurePerksGrantedExist();
        EnsureRewardCatalogValid();
        _ = new RoomLifetimeSettings(Rooms);
        _ = new WorldEventRules(WorldEvents);
    }

    /// <summary>A prize can only give a perk that is in the list (the built-in set when no list was given), and its rewards must be sane.</summary>
    private void EnsurePerksGrantedExist()
    {
        var known = (Perks.Perks.Count > 0 ? Perks.Perks : PerkOptions.BuiltIn).Select(perk => perk.Id).ToHashSet();
        foreach (var prize in Prize.Definitions()) EnsureRewardValid($"Prize '{prize.Id}'", prize.Reward, known);
        EnsureRewardValid("The turret", WorldEvents.Turret.Reward, known);
        EnsureRewardValid("The bounty", WorldEvents.Bounty.Reward, known);
        EnsureRewardValid("The maze prize", WorldEvents.Maze.PrizeReward, known);
        foreach (var prize in Prize.Definitions().Where(prize => !string.IsNullOrWhiteSpace(prize.GrantsPerk) && !known.Contains(prize.GrantsPerk!)))
            throw new ArgumentException($"Prize '{prize.Id}' gives the perk '{prize.GrantsPerk}', which does not exist.");
        var turretPerk = WorldEvents.Turret.GrantsPerk;
        if (!string.IsNullOrWhiteSpace(turretPerk) && !known.Contains(turretPerk))
            throw new ArgumentException($"The turret gives the perk '{turretPerk}', which does not exist.");
        var mazePerk = WorldEvents.Maze.PrizeGrantsPerk;
        if (!string.IsNullOrWhiteSpace(mazePerk) && !known.Contains(mazePerk))
            throw new ArgumentException($"The maze prize gives the perk '{mazePerk}', which does not exist.");
    }

    /// <summary>The catalog holds sane rewards under unique ids, and everything that picks a reward picks one that is in it.</summary>
    private void EnsureRewardCatalogValid()
    {
        var known = (Perks.Perks.Count > 0 ? Perks.Perks : PerkOptions.BuiltIn).Select(perk => perk.Id).ToHashSet();
        foreach (var reward in Rewards)
        {
            if (string.IsNullOrWhiteSpace(reward.Id)) throw new ArgumentException("A reward needs an id.");
            if (reward.Id.Length > MaxRewardIdLength) throw new ArgumentException($"Reward '{reward.Id}': the id is longer than {MaxRewardIdLength} characters.");
            if (string.IsNullOrWhiteSpace(reward.Name)) throw new ArgumentException($"Reward '{reward.Id}' needs a name.");
            EnsureRewardValid($"Reward '{reward.Id}'", reward.Reward, known);
        }
        var repeated = Rewards.GroupBy(reward => reward.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null) throw new ArgumentException($"Two rewards share the id '{repeated.Key}'.");
        var picks = Prize.Definitions().Select(prize => ($"Prize '{prize.Id}'", prize.RewardIds))
            .Append(("The turret", WorldEvents.Turret.RewardIds)).Append(("The bounty", WorldEvents.Bounty.RewardIds)).Append(("The maze prize", WorldEvents.Maze.RewardIds));
        foreach (var (owner, ids) in picks)
            foreach (var id in ids.Where(id => !Rewards.Any(reward => SameId(reward.Id, id))))
                throw new ArgumentException($"{owner} gives the reward '{id}', which does not exist.");
    }

    /// <summary>The longest reward id the database keeps.</summary>
    public const int MaxRewardIdLength = 64;

    private static void EnsureRewardValid(string owner, PerkReward reward, HashSet<string> known)
    {
        if (reward.Problem() is { } problem) throw new ArgumentException($"{owner}: {problem}.");
        if (!string.IsNullOrWhiteSpace(reward.TierSurgePerk) && !known.Contains(reward.TierSurgePerk))
            throw new ArgumentException($"{owner} lifts the perk '{reward.TierSurgePerk}', which does not exist.");
    }
}

/// <summary>A config and the revision it was saved as. A game server applies a revision it has not seen.</summary>
public sealed record GameplayDocument(string Revision, GameplayConfig Config);
