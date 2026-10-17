namespace Domain.Game.Rules;

/// <summary>Maps mass (eaten fruit value) to body length and advances a body along its path.</summary>
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

    /// <summary>
    /// Appends the new head (last element) and trims the tail down to the target length.
    /// A longer target grows the body by one segment per call; a shorter one shrinks it at once.
    /// </summary>
    public IReadOnlyList<T> Advance<T>(IEnumerable<T> path, T newHead, int targetLength)
    {
        ArgumentNullException.ThrowIfNull(path);
        EnsureAtLeastOne(targetLength, nameof(targetLength));

        var extended = path.Append(newHead).ToArray();
        return extended.Skip(Math.Max(0, extended.Length - targetLength)).ToArray();
    }

    private static void EnsureAtLeastOne(int value, string name)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(name, value, "Must be at least 1.");
    }
}
