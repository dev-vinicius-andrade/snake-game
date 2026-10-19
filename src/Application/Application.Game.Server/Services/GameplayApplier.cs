using System.Text.Json;
using Domain.Game.Rules;

namespace Application.Game.Server.Services;

/// <summary>
/// Puts the gameplay config the manager serves onto the running rules. A revision is applied once; a config that does not
/// hold up is refused whole, so the rules never end up half changed.
/// </summary>
public sealed class GameplayApplier(PerkRules perks, PrizeRules prize, RoomLifetimeSettings roomLifetime, Serilog.ILogger? logger = null)
{
    /// <summary>The revision now in force, or null while the server runs on its own settings.</summary>
    public string? Revision { get; private set; }

    /// <summary>True when a new revision was applied.</summary>
    public bool Apply(string? json)
    {
        var document = Read(json);
        if (document is null || document.Revision == Revision) return false;
        try
        {
            document.Config.EnsureValid();
            perks.Reconfigure(document.Config.Perks);
            prize.Reconfigure(document.Config.Prize);
            roomLifetime.Reconfigure(document.Config.Rooms);
        }
        catch (ArgumentException exception)
        {
            logger?.Warning(exception, "Gameplay revision {Revision} was refused", document.Revision);
            return false;
        }
        Revision = document.Revision;
        logger?.Information("Gameplay revision {Revision} applied", Revision);
        return true;
    }

    private GameplayDocument? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var document = JsonSerializer.Deserialize<GameplayDocument>(json, GameplayConfig.Json);
            return document is { Config: not null } && !string.IsNullOrEmpty(document.Revision) ? document : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
