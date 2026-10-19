namespace Domain.Game.Rules;

/// <summary>A spot that is marked, and when it is struck.</summary>
public readonly record struct StormStrike(double X, double Y, double Radius, DateTimeOffset StrikesAt);

/// <summary>
/// Where a room is in its storms: when the next one is due (while none is going on), or what is left of the one that is:
/// the moments strikes are still to be marked, and the marked ones waiting to fall. Nothing set at all before the first look.
/// </summary>
public sealed record StormState(DateTimeOffset? NextAt, IReadOnlyList<DateTimeOffset> Waiting, IReadOnlyList<StormStrike> Pending)
{
    public static StormState None { get; } = new(null, [], []);

    public bool InProgress => Waiting.Count + Pending.Count > 0;
}

/// <param name="Started">Strikes that were marked this time: show them to the players.</param>
/// <param name="Fired">Strikes whose warning ran out this time: carry them out.</param>
public sealed record StormStep(StormState State, IReadOnlyList<StormStrike> Started, IReadOnlyList<StormStrike> Fired);

/// <summary>What a strike did: the body segments it destroyed (the head is not among them) and whether it reached the head.</summary>
public readonly record struct StrikeHit(bool HeadHit, IReadOnlyList<int> BrokenSegments);

/// <summary>
/// Decides when a room is hit by a storm (lightning, a meteor rain), what each strike of it is and where it lands, and what a
/// strike reaches. Pure: the caller supplies the clock and acts on the result. A new kind of event that comes as strikes only
/// needs settings (<see cref="IStormSettings"/>) and a <see cref="WorldEventKind"/>.
/// </summary>
public sealed class WorldEventRules
{
    /// <summary>At most this many strikes in a storm, so a room never has more than this many marked at once.</summary>
    public const int MaxStrikesPerStorm = 40;

    /// <summary>A strike aimed at a snake lands within this share of its radius from the head: near enough to be felt, not always a hit.</summary>
    private const double AimSpread = 0.6;

    private readonly Func<double> _random;
    private volatile WorldEventOptions _options;

    /// <param name="random">Gives numbers from 0 up to 1, to draw intervals, counts and spots. Defaults to the shared generator.</param>
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
        if (options.Lightning is null || options.Meteors is null) throw new ArgumentException("World event options are missing.", nameof(options));
        Validate("Lightning", options.Lightning);
        Validate("Meteors", options.Meteors);
        return options;
    }

    private static void Validate(string name, IStormSettings settings)
    {
        if (settings.MinIntervalSeconds < 1)
            throw OutOfRange(name, settings.MinIntervalSeconds, "the shortest interval must be at least 1 second");
        if (settings.MaxIntervalSeconds < settings.MinIntervalSeconds)
            throw OutOfRange(name, settings.MaxIntervalSeconds, "the longest interval must not be shorter than the shortest");
        if (settings.Radius is < 1 or > 1000) throw OutOfRange(name, settings.Radius, "the radius must be 1 to 1000");
        if (settings.TelegraphMilliseconds is < 100 or > 10000)
            throw OutOfRange(name, settings.TelegraphMilliseconds, "the warning must last 100 to 10000 milliseconds");
        if (settings.MinCount is < 1 or > MaxStrikesPerStorm)
            throw OutOfRange(name, settings.MinCount, $"the fewest strikes must be 1 to {MaxStrikesPerStorm}");
        if (settings.MaxCount < settings.MinCount || settings.MaxCount > MaxStrikesPerStorm)
            throw OutOfRange(name, settings.MaxCount, $"the most strikes must be from the fewest up to {MaxStrikesPerStorm}");
        if (settings.DurationSeconds is < 1 or > 120) throw OutOfRange(name, settings.DurationSeconds, "the storm must last 1 to 120 seconds");
        if (!(settings.AimedShare >= 0 && settings.AimedShare <= 1)) throw OutOfRange(name, settings.AimedShare, "the aimed share must be 0 to 1");
        if (settings.Rooms.Concat(settings.ExcludedRooms).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"{name} has a blank room.", nameof(settings));
    }

    private static ArgumentOutOfRangeException OutOfRange(string name, object value, string reason)
        => new("options", value, $"{name}: {reason}.");

    /// <summary>
    /// Moves a room's storms of one kind on. Call as often as the warnings need to be timed (every frame is fine). Whether the room
    /// is one the event is for, and whether anyone is in it, is for the caller to decide: when not, it forgets the state.
    /// </summary>
    /// <param name="heads">Where the living snakes' heads are, for the strikes that are aimed.</param>
    public StormStep Step(WorldEventKind kind, StormState state, DateTimeOffset now, double roomWidth, double roomHeight, IReadOnlyList<Point> heads)
    {
        var options = _options;
        var settings = options.Of(kind);
        if (!options.Enabled || !settings.Enabled) return new StormStep(StormState.None, [], []);
        if (!state.InProgress)
        {
            if (state.NextAt is not { } next) return new StormStep(Scheduled(settings, now), [], []);
            if (now < next) return new StormStep(state, [], []);
            state = Begin(settings, now);
        }

        var fired = state.Pending.Where(strike => strike.StrikesAt <= now).ToList();
        var pending = state.Pending.Where(strike => strike.StrikesAt > now).ToList();
        var due = state.Waiting.TakeWhile(moment => moment <= now).Count();
        var started = Enumerable.Range(0, due).Select(_ => Spot(settings, now, roomWidth, roomHeight, heads)).ToList();
        pending.AddRange(started);
        var waiting = state.Waiting.Skip(due).ToList();
        return waiting.Count == 0 && pending.Count == 0
            ? new StormStep(Scheduled(settings, now), started, fired)
            : new StormStep(new StormState(null, waiting, pending), started, fired);
    }

    private StormState Scheduled(IStormSettings settings, DateTimeOffset now)
        => new(now.AddSeconds(settings.MinIntervalSeconds + (settings.MaxIntervalSeconds - settings.MinIntervalSeconds) * _random()), [], []);

    /// <summary>A storm: how many strikes, and the moments, spread over its duration, at which each is marked.</summary>
    private StormState Begin(IStormSettings settings, DateTimeOffset now)
    {
        var count = Math.Min(settings.MinCount + (int)(_random() * (settings.MaxCount - settings.MinCount + 1)), settings.MaxCount);
        var moments = Enumerable.Range(0, count).Select(_ => now.AddSeconds(_random() * settings.DurationSeconds)).Order().ToArray();
        return new StormState(null, moments, []);
    }

    private StormStrike Spot(IStormSettings settings, DateTimeOffset now, double width, double height, IReadOnlyList<Point> heads)
    {
        var strikesAt = now.AddMilliseconds(settings.TelegraphMilliseconds);
        if (heads.Count > 0 && _random() < settings.AimedShare)
        {
            var head = heads[Math.Min((int)(_random() * heads.Count), heads.Count - 1)];
            var angle = _random() * 2 * Math.PI;
            var distance = _random() * settings.Radius * AimSpread;
            return new StormStrike(
                Math.Clamp(head.X + Math.Cos(angle) * distance, 0, width), Math.Clamp(head.Y + Math.Sin(angle) * distance, 0, height),
                settings.Radius, strikesAt);
        }
        return new StormStrike(_random() * width, _random() * height, settings.Radius, strikesAt);
    }

    /// <summary>
    /// What a strike at <paramref name="center"/> reaches of a body (tail first, head last). A segment is a square of
    /// <paramref name="headSize"/> anchored at its point, and counts when its middle is within the radius. The body is never
    /// broken below <paramref name="minLength"/> segments: the ones nearest to the centre go first.
    /// </summary>
    public static StrikeHit Resolve(IReadOnlyList<Point> segments, Point center, double radius, double headSize, int minLength)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0) return new StrikeHit(false, []);

        bool Reaches(Point point) => DistanceToMiddle(point, center, headSize) <= radius;
        var headHit = Reaches(segments[^1]);
        var allowed = Math.Max(0, segments.Count - Math.Max(minLength, 1));
        var broken = Enumerable.Range(0, segments.Count - 1)
            .Where(index => Reaches(segments[index]))
            .OrderBy(index => DistanceToMiddle(segments[index], center, headSize))
            .Take(allowed)
            .OrderBy(index => index)
            .ToArray();
        return new StrikeHit(headHit, broken);
    }

    private static double DistanceToMiddle(Point segment, Point center, double headSize)
    {
        var dx = segment.X + headSize / 2 - center.X;
        var dy = segment.Y + headSize / 2 - center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
