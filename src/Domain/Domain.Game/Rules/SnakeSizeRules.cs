namespace Domain.Game.Rules;

/// <summary>Maps mass (eaten fruit value) to body length.</summary>
public sealed class SnakeSizeRules
{
    private readonly SnakeRulesOptions _options;

    public SnakeSizeRules(SnakeRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        _options = options;
    }

    /// <summary>initialLength + floor(max(mass, 0) / massPerSegment).</summary>
    public int TargetLength(int initialLength, long mass)
    {
        EnsureAtLeastOne(initialLength, nameof(initialLength));
        return initialLength + (int)Math.Floor(Math.Max(mass, 0) / _options.MassPerSegment);
    }

    private static void EnsureAtLeastOne(int value, string name)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(name, value, "Must be at least 1.");
    }
}
