using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

public enum PrizeAction
{
    None,
    /// <summary>Put a prize on the map.</summary>
    Spawn,
    /// <summary>Take the unclaimed prize off the map.</summary>
    Expire
}

/// <summary>When the next prize of a kind is due, or when the one on the map runs out. Both null before the first look.</summary>
public readonly record struct PrizeState(DateTimeOffset? NextSpawnAt, DateTimeOffset? ExpiresAt)
{
    public static PrizeState None { get; } = new(null, null);
}

public readonly record struct PrizeStep(PrizeState State, PrizeAction Action);

/// <summary>Decides when a room gets a prize of each kind. Pure: the caller supplies the clock and acts on the result.</summary>
public sealed class PrizeRules
{
    public const int MaxKinds = 10;

    private volatile Settings _settings;

    private sealed record Settings(PrizeOptions Options, IReadOnlyList<PrizeDefinition> Definitions);

    public PrizeRules(PrizeOptions options)
    {
        _settings = Build(options);
    }

    /// <summary>Switches to other options while the game runs. Options that do not hold up are refused and the old ones stay.</summary>
    public void Reconfigure(PrizeOptions options) => _settings = Build(options);

    /// <summary>The options in use.</summary>
    public PrizeOptions Options => _settings.Options;

    /// <summary>The kinds of prize in force.</summary>
    public IReadOnlyList<PrizeDefinition> Definitions => _settings.Definitions;

    private static Settings Build(PrizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var definitions = options.Definitions();
        if (definitions.Count > MaxKinds) throw new ArgumentException($"At most {MaxKinds} kinds of prize.", nameof(options));
        var repeated = definitions.GroupBy(definition => definition.Id).FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null) throw new ArgumentException($"Prize id '{repeated.Key}' is used twice.", nameof(options));
        foreach (var definition in definitions) EnsureValid(definition);
        return new Settings(options, definitions);
    }

    private static void EnsureValid(PrizeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id)) throw new ArgumentException("A prize needs an id.", nameof(definition));
        if (definition.Id == MazeOptions.PrizeId)
            throw new ArgumentException($"The prize id '{MazeOptions.PrizeId}' is the maze's own.", nameof(definition));
        EnsurePositive(definition.SpawnIntervalSeconds, nameof(definition.SpawnIntervalSeconds));
        EnsurePositive(definition.LifetimeSeconds, nameof(definition.LifetimeSeconds));
        EnsurePositive(definition.Value, nameof(definition.Value));
        EnsurePositive(definition.MinPlayers, nameof(definition.MinPlayers));
        if (definition.FoodsCredit < 0)
            throw new ArgumentOutOfRangeException(nameof(definition), definition.FoodsCredit, $"Prize '{definition.Id}': foods credit must not be negative.");
        if (!IsHexColor(definition.Color))
            throw new ArgumentException($"Prize '{definition.Id}': the colour must be six hex digits such as #ffd23f.", nameof(definition));
        if (definition.Rooms.Concat(definition.ExcludedRooms).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Prize '{definition.Id}' has a blank room.", nameof(definition));
    }

    private static bool IsHexColor(string? color)
        => color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);

    public PrizeStep Step(PrizeDefinition definition, PrizeState state, DateTimeOffset now, bool prizePresent, int players)
    {
        if (!_settings.Options.Enabled || !definition.Enabled) return new PrizeStep(state, PrizeAction.None);
        return prizePresent ? WhilePresent(state, now, definition) : WhileAbsent(state, now, players, definition);
    }

    private static PrizeStep WhilePresent(PrizeState state, DateTimeOffset now, PrizeDefinition definition)
        => state.ExpiresAt <= now
            ? new PrizeStep(ScheduledFrom(now, definition), PrizeAction.Expire)
            : new PrizeStep(state, PrizeAction.None);

    private static PrizeStep WhileAbsent(PrizeState state, DateTimeOffset now, int players, PrizeDefinition definition)
    {
        if (state.ExpiresAt is not null || state.NextSpawnAt is null) return new PrizeStep(ScheduledFrom(now, definition), PrizeAction.None);
        var due = now >= state.NextSpawnAt && players >= definition.MinPlayers;
        return due
            ? new PrizeStep(new PrizeState(null, now.AddSeconds(definition.LifetimeSeconds)), PrizeAction.Spawn)
            : new PrizeStep(state, PrizeAction.None);
    }

    private static PrizeState ScheduledFrom(DateTimeOffset now, PrizeDefinition definition)
        => new(now.AddSeconds(definition.SpawnIntervalSeconds), null);

    private static void EnsurePositive(long value, string name)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(name, value, "Must be at least 1.");
    }
}
