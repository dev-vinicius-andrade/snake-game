namespace Domain.Game.Rules;

public readonly record struct BoostResult(bool Active, int BoostTicks, long MassSpent, double SpeedMultiplier);

/// <summary>Boost speeds a snake up and burns one mass every <see cref="SnakeRulesOptions.BoostTicksPerMass"/> ticks.</summary>
public sealed class BoostRules
{
    private readonly SnakeRulesOptions _options;

    public BoostRules(SnakeRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        _options = options;
    }

    /// <param name="requested">Player holds the boost control.</param>
    /// <param name="boostTicks">Ticks boosted since the last mass was spent.</param>
    /// <param name="mass">Current mass; at least 1 is needed to boost.</param>
    public BoostResult Step(bool requested, int boostTicks, long mass)
    {
        if (boostTicks < 0) throw new ArgumentOutOfRangeException(nameof(boostTicks), boostTicks, "Must not be negative.");
        if (!requested || mass < 1) return new BoostResult(false, 0, 0, 1.0);

        var ticks = boostTicks + 1;
        return ticks >= _options.BoostTicksPerMass
            ? new BoostResult(true, 0, 1, _options.BoostSpeedMultiplier)
            : new BoostResult(true, ticks, 0, _options.BoostSpeedMultiplier);
    }
}
