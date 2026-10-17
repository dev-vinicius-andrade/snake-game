namespace Domain.Game.Rules;

/// <summary>Tunable constants for snake size, collisions and death drops. See docs/GAME_DESIGN.md.</summary>
public sealed record SnakeRulesOptions
{
    public const string SectionName = "SnakeRules";

    /// <summary>Mass (eaten fruit value) needed for one extra body segment.</summary>
    public double MassPerSegment { get; init; } = 1.0;

    /// <summary>Upper bound of pellet entities a dying snake leaves behind.</summary>
    public int MaxDeathPellets { get; init; } = 40;

    public void EnsureValid()
    {
        if (MassPerSegment <= 0)
            throw new ArgumentOutOfRangeException(nameof(MassPerSegment), MassPerSegment, "Must be greater than zero.");
        if (MaxDeathPellets < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxDeathPellets), MaxDeathPellets, "Must be at least 1.");
    }
}
