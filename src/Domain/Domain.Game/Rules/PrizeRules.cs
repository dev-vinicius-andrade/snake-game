namespace Domain.Game.Rules;

public enum PrizeAction
{
    None,
    /// <summary>Put a prize on the map.</summary>
    Spawn,
    /// <summary>Take the unclaimed prize off the map.</summary>
    Expire
}

/// <summary>When the next prize is due, or when the one on the map runs out. Both null before the first look.</summary>
public readonly record struct PrizeState(DateTimeOffset? NextSpawnAt, DateTimeOffset? ExpiresAt)
{
    public static PrizeState None { get; } = new(null, null);
}

public readonly record struct PrizeStep(PrizeState State, PrizeAction Action);

/// <summary>Decides when a room gets a prize. Pure: the caller supplies the clock and acts on the result.</summary>
public sealed class PrizeRules
{
    private volatile PrizeOptions _options;

    public PrizeRules(PrizeOptions options)
    {
        _options = Validated(options);
    }

    /// <summary>Switches to other options while the game runs. Options that do not hold up are refused and the old ones stay.</summary>
    public void Reconfigure(PrizeOptions options) => _options = Validated(options);

    /// <summary>The options in use.</summary>
    public PrizeOptions Options => _options;

    private static PrizeOptions Validated(PrizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsurePositive(options.SpawnIntervalSeconds, nameof(options.SpawnIntervalSeconds));
        EnsurePositive(options.LifetimeSeconds, nameof(options.LifetimeSeconds));
        EnsurePositive(options.Value, nameof(options.Value));
        EnsurePositive(options.MinPlayers, nameof(options.MinPlayers));
        if (options.FoodsCredit < 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.FoodsCredit, "Foods credit must not be negative.");
        return options;
    }

    public PrizeStep Step(PrizeState state, DateTimeOffset now, bool prizePresent, int players)
    {
        var options = _options;
        if (!options.Enabled) return new PrizeStep(state, PrizeAction.None);
        return prizePresent ? WhilePresent(state, now, options) : WhileAbsent(state, now, players, options);
    }

    private static PrizeStep WhilePresent(PrizeState state, DateTimeOffset now, PrizeOptions options)
        => state.ExpiresAt <= now
            ? new PrizeStep(ScheduledFrom(now, options), PrizeAction.Expire)
            : new PrizeStep(state, PrizeAction.None);

    private static PrizeStep WhileAbsent(PrizeState state, DateTimeOffset now, int players, PrizeOptions options)
    {
        if (state.ExpiresAt is not null || state.NextSpawnAt is null) return new PrizeStep(ScheduledFrom(now, options), PrizeAction.None);
        var due = now >= state.NextSpawnAt && players >= options.MinPlayers;
        return due
            ? new PrizeStep(new PrizeState(null, now.AddSeconds(options.LifetimeSeconds)), PrizeAction.Spawn)
            : new PrizeStep(state, PrizeAction.None);
    }

    private static PrizeState ScheduledFrom(DateTimeOffset now, PrizeOptions options)
        => new(now.AddSeconds(options.SpawnIntervalSeconds), null);

    private static void EnsurePositive(long value, string name)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(name, value, "Must be at least 1.");
    }
}
