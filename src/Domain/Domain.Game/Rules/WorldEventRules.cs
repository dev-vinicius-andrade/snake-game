namespace Domain.Game.Rules;

public enum LightningAction
{
    None,
    /// <summary>A spot was chosen: show it to the players.</summary>
    Telegraph,
    /// <summary>The warning ran out: strike the spot.</summary>
    Strike
}

/// <summary>A spot that is marked, and when it is struck.</summary>
public readonly record struct LightningStrike(double X, double Y, double Radius, DateTimeOffset StrikesAt);

/// <summary>When the next warning is due, or the strike that is waiting for its moment. Both null before the first look.</summary>
public readonly record struct LightningState(DateTimeOffset? NextAt, LightningStrike? Pending)
{
    public static LightningState None { get; } = new(null, null);
}

/// <param name="Strike">The strike to show (Telegraph) or to carry out (Strike).</param>
public readonly record struct LightningStep(LightningState State, LightningAction Action, LightningStrike? Strike = null);

/// <summary>What a strike did: the body segments it destroyed (the head is not among them) and whether it reached the head.</summary>
public readonly record struct LightningHit(bool HeadHit, IReadOnlyList<int> BrokenSegments);

/// <summary>
/// Decides when a room is struck by lightning, and what a strike reaches. Pure: the caller supplies the clock and acts on the result.
/// More events (earthquakes, ...) are to get their own state and step here, sharing the options.
/// </summary>
public sealed class WorldEventRules
{
    private readonly Func<double> _random;
    private volatile WorldEventOptions _options;

    /// <param name="random">Gives numbers from 0 up to 1, to draw intervals and spots. Defaults to the shared generator.</param>
    public WorldEventRules(WorldEventOptions options, Func<double>? random = null)
    {
        _options = Validated(options);
        _random = random ?? Random.Shared.NextDouble;
    }

    /// <summary>The options in use.</summary>
    public WorldEventOptions Options => _options;

    /// <summary>Switches to other options while the game runs. Options that do not hold up are refused and the old ones stay.</summary>
    public void Reconfigure(WorldEventOptions options) => _options = Validated(options);

    private static WorldEventOptions Validated(WorldEventOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var lightning = options.Lightning ?? throw new ArgumentException("Lightning options are missing.", nameof(options));
        if (lightning.MinIntervalSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(options), lightning.MinIntervalSeconds, "Lightning: the shortest interval must be at least 1 second.");
        if (lightning.MaxIntervalSeconds < lightning.MinIntervalSeconds)
            throw new ArgumentOutOfRangeException(nameof(options), lightning.MaxIntervalSeconds, "Lightning: the longest interval must not be shorter than the shortest.");
        if (lightning.Radius is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options), lightning.Radius, "Lightning: the radius must be 1 to 1000.");
        if (lightning.TelegraphMilliseconds is < 100 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(options), lightning.TelegraphMilliseconds, "Lightning: the warning must last 100 to 10000 milliseconds.");
        if (lightning.Rooms.Concat(lightning.ExcludedRooms).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Lightning has a blank room.", nameof(options));
        return options;
    }

    /// <summary>
    /// Moves a room's lightning on. Call as often as the warning needs to be timed (every frame is fine). Whether the room
    /// is one it strikes in, and whether anyone is in it, is for the caller to decide: when not, it forgets the state.
    /// </summary>
    public LightningStep Step(LightningState state, DateTimeOffset now, double roomWidth, double roomHeight)
    {
        var options = _options;
        if (!options.Enabled || !options.Lightning.Enabled) return new LightningStep(LightningState.None, LightningAction.None);
        if (state.Pending is { } pending)
            return now >= pending.StrikesAt
                ? new LightningStep(Scheduled(now), LightningAction.Strike, pending)
                : new LightningStep(state, LightningAction.None);
        if (state.NextAt is not { } next) return new LightningStep(Scheduled(now), LightningAction.None);
        if (now < next) return new LightningStep(state, LightningAction.None);

        var strike = new LightningStrike(
            _random() * roomWidth, _random() * roomHeight, options.Lightning.Radius, now.AddMilliseconds(options.Lightning.TelegraphMilliseconds));
        return new LightningStep(new LightningState(null, strike), LightningAction.Telegraph, strike);
    }

    private LightningState Scheduled(DateTimeOffset now)
    {
        var lightning = _options.Lightning;
        var seconds = lightning.MinIntervalSeconds + (lightning.MaxIntervalSeconds - lightning.MinIntervalSeconds) * _random();
        return new LightningState(now.AddSeconds(seconds), null);
    }

    /// <summary>
    /// What a strike at <paramref name="center"/> reaches of a body (tail first, head last). A segment is a square of
    /// <paramref name="headSize"/> anchored at its point, and counts when its middle is within the radius. The body is never
    /// broken below <paramref name="minLength"/> segments: the ones nearest to the centre go first.
    /// </summary>
    public static LightningHit Resolve(IReadOnlyList<Point> segments, Point center, double radius, double headSize, int minLength)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0) return new LightningHit(false, []);

        bool Reaches(Point point) => DistanceToMiddle(point, center, headSize) <= radius;
        var headHit = Reaches(segments[^1]);
        var allowed = Math.Max(0, segments.Count - Math.Max(minLength, 1));
        var broken = Enumerable.Range(0, segments.Count - 1)
            .Where(index => Reaches(segments[index]))
            .OrderBy(index => DistanceToMiddle(segments[index], center, headSize))
            .Take(allowed)
            .OrderBy(index => index)
            .ToArray();
        return new LightningHit(headHit, broken);
    }

    private static double DistanceToMiddle(Point segment, Point center, double headSize)
    {
        var dx = segment.X + headSize / 2 - center.X;
        var dy = segment.Y + headSize / 2 - center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
