using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Game.Rules;

/// <summary>
/// Everything an administrator can tune while the game runs: the perks and the prize. The manager keeps it and the game
/// servers fetch it, so both sides read and write it the same way (<see cref="Json"/>).
/// </summary>
public sealed record GameplayConfig
{
    public PerkOptions Perks { get; init; } = new();
    public PrizeOptions Prize { get; init; } = new();

    /// <summary>How the config is written and read: camel case names, and effects by name.</summary>
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The built-in perks listed out, so an editor shows them, and the default prize.</summary>
    public static GameplayConfig Defaults => new GameplayConfig().WithBuiltInPerks();

    /// <summary>An empty perk list means the built-in set; this spells it out so it can be edited.</summary>
    public GameplayConfig WithBuiltInPerks()
        => Perks.Perks.Count > 0 ? this : this with { Perks = Perks with { Perks = PerkOptions.BuiltIn.ToList() } };

    /// <summary>Throws when the perks or the prize could not run.</summary>
    public void EnsureValid()
    {
        _ = new PerkRules(Perks);
        _ = new PrizeRules(Prize);
    }
}

/// <summary>A config and the revision it was saved as. A game server applies a revision it has not seen.</summary>
public sealed record GameplayDocument(string Revision, GameplayConfig Config);
