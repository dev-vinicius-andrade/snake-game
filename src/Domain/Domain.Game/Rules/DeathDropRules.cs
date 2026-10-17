namespace Domain.Game.Rules;

public readonly record struct PelletDrop(Point Position, long Value);

/// <summary>Turns the mass of a dead snake into a bounded number of pellets along its body.</summary>
public sealed class DeathDropRules
{
    private readonly SnakeRulesOptions _options;

    public DeathDropRules(SnakeRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        _options = options;
    }

    /// <summary>
    /// Pellet count is min(mass, max pellets); values are split evenly, the remainder goes one by one to the first
    /// pellets, so the total value always equals the mass. Positions are evenly spread over the body.
    /// </summary>
    public IReadOnlyList<PelletDrop> Plan(long mass, IReadOnlyList<Point> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (mass <= 0 || body.Count == 0) return Array.Empty<PelletDrop>();

        var count = (int)Math.Min(mass, _options.MaxDeathPellets);
        return Enumerable.Range(0, count)
            .Select(index => new PelletDrop(body[index * body.Count / count], ValueOf(index, mass, count)))
            .ToArray();
    }

    private static long ValueOf(int index, long mass, int count)
        => mass / count + (index < mass % count ? 1 : 0);
}
