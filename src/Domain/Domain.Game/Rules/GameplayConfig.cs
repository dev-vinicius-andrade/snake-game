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
    public static GameplayConfig Defaults => new GameplayConfig().WithBuiltInPerks();

    /// <summary>An empty perk list means the built-in set; this spells it out so it can be edited. Perks come out normalized.</summary>
    public GameplayConfig WithBuiltInPerks()
        => (Perks.Perks.Count > 0 ? this : this with { Perks = Perks with { Perks = PerkOptions.BuiltIn.ToList() } }).Normalized();

    /// <summary>Every perk with its effects in the list, the single old effect folded into it. Nothing else changes.</summary>
    public GameplayConfig Normalized() => this with
    {
        Perks = Perks with
        {
            Perks = Perks.Perks.Select(perk => perk with { Effects = perk.AllEffects().ToList(), Effect = null, Amount = 0 }).ToList()
        },
        Prize = Prize with { Prizes = Prize.Definitions().ToList() }
    };

    /// <summary>Throws when the perks or the prize could not run.</summary>
    public void EnsureValid()
    {
        _ = new PerkRules(Perks);
        _ = new PrizeRules(Prize);
        EnsurePerksGrantedExist();
        _ = new RoomLifetimeSettings(Rooms);
        _ = new WorldEventRules(WorldEvents);
    }

    /// <summary>A prize can only give a perk that is in the list (the built-in set when no list was given).</summary>
    private void EnsurePerksGrantedExist()
    {
        var known = (Perks.Perks.Count > 0 ? Perks.Perks : PerkOptions.BuiltIn).Select(perk => perk.Id).ToHashSet();
        foreach (var prize in Prize.Definitions().Where(prize => !string.IsNullOrWhiteSpace(prize.GrantsPerk) && !known.Contains(prize.GrantsPerk!)))
            throw new ArgumentException($"Prize '{prize.Id}' gives the perk '{prize.GrantsPerk}', which does not exist.");
    }
}

/// <summary>A config and the revision it was saved as. A game server applies a revision it has not seen.</summary>
public sealed record GameplayDocument(string Revision, GameplayConfig Config);
