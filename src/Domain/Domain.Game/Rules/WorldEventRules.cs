using Library.Commons.Game.Domain.Entities;

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

/// <summary>Where the turret of a room is in its life: when the next is due (while there is none), or when this one runs out and fires next.</summary>
public sealed record TurretState(
    DateTimeOffset? NextSpawnAt, DateTimeOffset? ExpiresAt, DateTimeOffset? NextFireAt, double Angle, double X, double Y)
{
    public static TurretState None { get; } = new(null, null, null, 0, 0, 0);

    public bool IsActive => ExpiresAt is not null;
}

public enum TurretAction
{
    None,
    /// <summary>Put the turret on the map at the position of the state.</summary>
    Spawn,
    /// <summary>Its time is up: take it off the map.</summary>
    Expire,
    /// <summary>Fire a shot along each of the angles.</summary>
    Fire
}

/// <param name="Angles">For Fire: the direction of each shot, in radians, 0 to the right and turning clockwise on screen.</param>
public sealed record TurretStep(TurretState State, TurretAction Action, IReadOnlyList<double> Angles);

/// <summary>
/// Where the safe zone of a room is in its life: when the next is due (while there is none), or the moments the one that is going
/// closes, stops closing and goes, and when it next makes a snake outside pay.
/// </summary>
public sealed record ZoneState(
    DateTimeOffset? NextAt, DateTimeOffset? ClosingAt, DateTimeOffset? ClosedAt, DateTimeOffset? EndsAt, DateTimeOffset? NextDamageAt,
    double X, double Y, double StartRadius)
{
    public static ZoneState None { get; } = new(null, null, null, null, null, 0, 0, 0);

    public bool IsActive => EndsAt is not null;
}

public enum ZonePhase
{
    /// <summary>The safe spot is shown and the zone covers everything: nobody is harmed.</summary>
    Warning,
    /// <summary>The zone is closing in and snakes outside pay.</summary>
    Closing,
    /// <summary>The zone is at its smallest.</summary>
    Holding
}

public enum ZoneAction
{
    None,
    /// <summary>The zone began: its state says where.</summary>
    Begin,
    /// <summary>The zone is over.</summary>
    End,
    /// <summary>Make every snake outside the zone, at the radius of the step, pay.</summary>
    Damage
}

/// <param name="Radius">For Damage: how far from the middle the zone reaches now.</param>
public sealed record ZoneStep(ZoneState State, ZoneAction Action, double Radius);

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

    /// <summary>At most this many mines lie on the map of a room at once, so snapshots stay small.</summary>
    public const int MaxActiveMines = 40;

    /// <summary>A strike aimed at a snake lands within this share of its radius from the head: near enough to be felt, not always a hit.</summary>
    private const double AimSpread = 0.6;

    /// <summary>How long a turret sits quiet after it appears, so players can see it and get ready before the first volley.</summary>
    public static readonly TimeSpan TurretWarmUp = TimeSpan.FromSeconds(3);

    private readonly Func<double> _random;
    private volatile WorldEventOptions _options;

    /// <param name="random">Gives numbers from 0 up to 1, to draw intervals, counts and spots. Defaults to the shared generator.</param>
    public WorldEventRules(WorldEventOptions options, Func<double>? random = null)
    {
        _options = Validated(options);
        _random = random ?? Random.Shared.NextDouble;
    }

    /// <summary>The options in use: what the administrator set for every room.</summary>
    public WorldEventOptions Options => _options;

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<RoomGameplay, Tuple<WorldEventOptions, WorldEventOptions>> _perRoom = new();

    /// <summary>
    /// The options for one room: the administrator's, with what whoever made the room picked put over them (see
    /// <see cref="WorldEventPresets"/>). Worked out once for as long as neither the choices nor the administrator's options change.
    /// </summary>
    public WorldEventOptions OptionsFor(Library.Commons.Game.Domain.Interfaces.Entities.IRoom? room)
    {
        var admin = _options;
        var gameplay = room.GameplayOf();
        if (gameplay.IsDefault) return admin;
        if (_perRoom.TryGetValue(gameplay, out var cached) && ReferenceEquals(cached.Item1, admin)) return cached.Item2;
        var resolved = WorldEventPresets.Apply(admin, gameplay);
        _perRoom.AddOrUpdate(gameplay, Tuple.Create(admin, resolved));
        return resolved;
    }

    /// <summary>Switches to other options while the game runs. Options that do not hold up are refused and the old ones stay.</summary>
    public void Reconfigure(WorldEventOptions options) => _options = Validated(options);

    private static WorldEventOptions Validated(WorldEventOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Lightning is null || options.Meteors is null) throw new ArgumentException("World event options are missing.", nameof(options));
        Validate("Lightning", options.Lightning);
        Validate("Meteors", options.Meteors);
        ValidateTurret(options.Turret ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        ValidateZone(options.Zone ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        ValidateFruitShower(options.FruitShower ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        ValidateBounty(options.Bounty ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        ValidateMines(options.Mines ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        ValidateSugarRush(options.SugarRush ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        ValidateMaze(options.Maze ?? throw new ArgumentException("World event options are missing.", nameof(options)));
        return options;
    }

    private static void ValidateSchedule(string name, IScheduledEvent settings, IEnumerable<string> rooms)
    {
        if (settings.MinIntervalSeconds < 1) throw OutOfRange(name, settings.MinIntervalSeconds, "the shortest interval must be at least 1 second");
        if (settings.MaxIntervalSeconds < settings.MinIntervalSeconds)
            throw OutOfRange(name, settings.MaxIntervalSeconds, "the longest interval must not be shorter than the shortest");
        if (settings.MinPlayers < 1) throw OutOfRange(name, settings.MinPlayers, "at least 1 player is needed");
        if (rooms.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException($"{name} has a blank room.", name);
    }

    private static void ValidateFruitShower(FruitShowerOptions shower)
    {
        ValidateSchedule("Fruit shower", shower, shower.Rooms.Concat(shower.ExcludedRooms));
        if (shower.DurationSeconds is < 1 or > 60) throw OutOfRange("Fruit shower", shower.DurationSeconds, "it must last 1 to 60 seconds");
        if (shower.FruitsPerSecond is < 1 or > 30) throw OutOfRange("Fruit shower", shower.FruitsPerSecond, "it must drop 1 to 30 fruits a second");
    }

    private static void ValidateBounty(BountyOptions bounty)
    {
        ValidateSchedule("Bounty", bounty, bounty.Rooms.Concat(bounty.ExcludedRooms));
        if (bounty.DurationSeconds is < 5 or > 3600) throw OutOfRange("Bounty", bounty.DurationSeconds, "it must last 5 to 3600 seconds");
        if (bounty.RewardMass is < 0 or > 10000) throw OutOfRange("Bounty", bounty.RewardMass, "the reward must be 0 to 10000 mass");
        if (bounty.FoodsCredit is < 0 or > 1000) throw OutOfRange("Bounty", bounty.FoodsCredit, "the foods credit must be 0 to 1000");
    }

    private static void ValidateMines(MineFieldOptions mines)
    {
        ValidateSchedule("Mines", mines, mines.Rooms.Concat(mines.ExcludedRooms));
        if (mines.MinMines is < 1 or > MaxActiveMines) throw OutOfRange("Mines", mines.MinMines, $"the fewest mines must be 1 to {MaxActiveMines}");
        if (mines.MaxMines < mines.MinMines || mines.MaxMines > MaxActiveMines)
            throw OutOfRange("Mines", mines.MaxMines, $"the most mines must be from the fewest up to {MaxActiveMines}");
        if (mines.Radius is < 1 or > 1000) throw OutOfRange("Mines", mines.Radius, "the blast radius must be 1 to 1000");
        if (mines.TriggerRadius is < 1 or > 200) throw OutOfRange("Mines", mines.TriggerRadius, "the trigger radius must be 1 to 200");
        if (mines.ArmMilliseconds is < 0 or > 60000) throw OutOfRange("Mines", mines.ArmMilliseconds, "arming must take 0 to 60000 milliseconds");
    }

    private static void ValidateMaze(MazeOptions maze)
    {
        ValidateSchedule("Maze", maze, maze.Rooms.Concat(maze.ExcludedRooms));
        if (maze.DurationSeconds is < 10 or > 3600) throw OutOfRange("Maze", maze.DurationSeconds, "it must stand 10 to 3600 seconds");
        if (maze.Columns is < MazeGenerator.MinCells or > MazeGenerator.MaxCells)
            throw OutOfRange("Maze", maze.Columns, $"it must be {MazeGenerator.MinCells} to {MazeGenerator.MaxCells} cells across");
        if (maze.Rows is < MazeGenerator.MinCells or > MazeGenerator.MaxCells)
            throw OutOfRange("Maze", maze.Rows, $"it must be {MazeGenerator.MinCells} to {MazeGenerator.MaxCells} cells down");
        if (maze.CorridorHeads is < MazeGenerator.MinCorridor or > MazeGenerator.MaxCorridor)
            throw OutOfRange("Maze", maze.CorridorHeads, $"a corridor must be {MazeGenerator.MinCorridor} to {MazeGenerator.MaxCorridor} head sizes wide, so a long snake can turn");
        if (maze.Twistiness is < 0 or > 100) throw OutOfRange("Maze", maze.Twistiness, "the twistiness must be 0 to 100");
        if (maze.WallMassLoss is < 0 or > 1000) throw OutOfRange("Maze", maze.WallMassLoss, "a wall must cost 0 to 1000 mass");
        if (maze.FoodPercent is < 0 or > 100) throw OutOfRange("Maze", maze.FoodPercent, "the share of cells with a fruit must be 0 to 100");
        if (maze.PrizeMass is < 1 or > 10000) throw OutOfRange("Maze", maze.PrizeMass, "the prize must be worth 1 to 10000 mass");
        if (maze.PrizeFoodsCredit is < 0 or > 1000) throw OutOfRange("Maze", maze.PrizeFoodsCredit, "the foods credit must be 0 to 1000");
        if (maze.PrizeColor is not { Length: 7 } color || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit))
            throw new ArgumentException("Maze: the prize colour must be six hex digits such as #ffd23f.", nameof(maze));
    }

    private static void ValidateSugarRush(SugarRushOptions rush)
    {
        ValidateSchedule("Sugar rush", rush, rush.Rooms.Concat(rush.ExcludedRooms));
        if (rush.DurationSeconds is < 1 or > 600) throw OutOfRange("Sugar rush", rush.DurationSeconds, "it must last 1 to 600 seconds");
    }

    private static void ValidateZone(ZoneOptions zone)
    {
        if (zone.MinIntervalSeconds < 1) throw OutOfRange("Zone", zone.MinIntervalSeconds, "the shortest interval must be at least 1 second");
        if (zone.MaxIntervalSeconds < zone.MinIntervalSeconds)
            throw OutOfRange("Zone", zone.MaxIntervalSeconds, "the longest interval must not be shorter than the shortest");
        if (zone.WarningSeconds is < 1 or > 600) throw OutOfRange("Zone", zone.WarningSeconds, "the warning must last 1 to 600 seconds");
        if (zone.ShrinkSeconds is < 5 or > 3600) throw OutOfRange("Zone", zone.ShrinkSeconds, "it must take 5 to 3600 seconds to close");
        if (zone.HoldSeconds is < 0 or > 3600) throw OutOfRange("Zone", zone.HoldSeconds, "it must hold for 0 to 3600 seconds");
        if (zone.FinalRadius is < 30 or > 2000) throw OutOfRange("Zone", zone.FinalRadius, "the final radius must be 30 to 2000");
        if (zone.DamageIntervalMilliseconds is < 200 or > 10000) throw OutOfRange("Zone", zone.DamageIntervalMilliseconds, "it must make snakes pay every 200 to 10000 milliseconds");
        if (zone.DamageMass is < 1 or > 1000) throw OutOfRange("Zone", zone.DamageMass, "the damage must be 1 to 1000 mass");
        if (zone.MinPlayers < 1) throw OutOfRange("Zone", zone.MinPlayers, "at least 1 player is needed");
        if (zone.Rooms.Concat(zone.ExcludedRooms).Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Zone has a blank room.", nameof(zone));
    }

    private static void ValidateTurret(TurretOptions turret)
    {
        if (turret.MinIntervalSeconds < 1) throw OutOfRange("Turret", turret.MinIntervalSeconds, "the shortest interval must be at least 1 second");
        if (turret.MaxIntervalSeconds < turret.MinIntervalSeconds)
            throw OutOfRange("Turret", turret.MaxIntervalSeconds, "the longest interval must not be shorter than the shortest");
        if (turret.LifetimeSeconds is < 10 or > 3600) throw OutOfRange("Turret", turret.LifetimeSeconds, "it must stay 10 to 3600 seconds");
        if (turret.HitPoints is < 1 or > 10000) throw OutOfRange("Turret", turret.HitPoints, "it must take 1 to 10000 hits");
        if (turret.Size is < 20 or > 300) throw OutOfRange("Turret", turret.Size, "its size must be 20 to 300");
        if (turret.FireIntervalMilliseconds is < 100 or > 10000) throw OutOfRange("Turret", turret.FireIntervalMilliseconds, "it must fire every 100 to 10000 milliseconds");
        if (turret.Arms is < 1 or > 16) throw OutOfRange("Turret", turret.Arms, "it must have 1 to 16 arms");
        if (!(Math.Abs(turret.SpinDegrees) <= 360)) throw OutOfRange("Turret", turret.SpinDegrees, "the spin must be from -360 to 360 degrees");
        if (!(turret.ShotSpeed >= 0.1 && turret.ShotSpeed <= 3)) throw OutOfRange("Turret", turret.ShotSpeed, "the shot speed must be 0.1 to 3");
        if (turret.MinPlayers < 1) throw OutOfRange("Turret", turret.MinPlayers, "at least 1 player is needed");
        if (turret.FoodsCredit is < 0 or > 1000) throw OutOfRange("Turret", turret.FoodsCredit, "the foods credit must be 0 to 1000");
        if (turret.RewardMass is < 0 or > 1000) throw OutOfRange("Turret", turret.RewardMass, "the reward must be 0 to 1000 mass");
        if (turret.Rooms.Concat(turret.ExcludedRooms).Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Turret has a blank room.", nameof(turret));
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
    /// <param name="forRoom">The options of the room (see <see cref="OptionsFor"/>); the administrator's when left out.</param>
    public StormStep Step(WorldEventKind kind, StormState state, DateTimeOffset now, double roomWidth, double roomHeight, IReadOnlyList<Point> heads, WorldEventOptions? forRoom = null)
    {
        var options = forRoom ?? _options;
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
    /// Moves the turret of a room on. Call as often as the volleys need to be timed (every frame is fine). Whether anyone is in the
    /// room is for the caller to say through <paramref name="players"/>; whether the turret was brought down, through
    /// <see cref="TurretKilled"/>.
    /// </summary>
    public TurretStep TurretStep(TurretState state, DateTimeOffset now, double roomWidth, double roomHeight, int players, WorldEventOptions? forRoom = null)
    {
        var options = forRoom ?? _options;
        var turret = options.Turret;
        if (!options.Enabled || !turret.Enabled) return new TurretStep(TurretState.None, TurretAction.None, []);
        if (!state.IsActive) return TurretWaiting(turret, state, now, roomWidth, roomHeight, players);
        if (now >= state.ExpiresAt) return new TurretStep(TurretScheduled(turret, now), TurretAction.Expire, []);
        if (now < state.NextFireAt) return new TurretStep(state, TurretAction.None, []);

        var angles = Enumerable.Range(0, turret.Arms).Select(arm => state.Angle + arm * 2 * Math.PI / turret.Arms).ToArray();
        var next = state with
        {
            Angle = state.Angle + turret.SpinDegrees * Math.PI / 180,
            NextFireAt = now.AddMilliseconds(turret.FireIntervalMilliseconds)
        };
        return new TurretStep(next, TurretAction.Fire, angles);
    }

    private TurretStep TurretWaiting(TurretOptions turret, TurretState state, DateTimeOffset now, double width, double height, int players)
    {
        if (state.NextSpawnAt is not { } due) return new TurretStep(TurretScheduled(turret, now), TurretAction.None, []);
        if (now < due || players < turret.MinPlayers) return new TurretStep(state, TurretAction.None, []);

        var margin = turret.Size / 2.0;
        var spawned = new TurretState(
            null, now.AddSeconds(turret.LifetimeSeconds), now.Add(TurretWarmUp), _random() * 2 * Math.PI,
            margin + _random() * Math.Max(0, width - turret.Size), margin + _random() * Math.Max(0, height - turret.Size));
        return new TurretStep(spawned, TurretAction.Spawn, []);
    }

    /// <summary>What the room is left with when its turret was brought down: the next one scheduled.</summary>
    public TurretState TurretKilled(DateTimeOffset now, WorldEventOptions? forRoom = null) => TurretScheduled((forRoom ?? _options).Turret, now);

    private TurretState TurretScheduled(TurretOptions turret, DateTimeOffset now)
        => TurretState.None with { NextSpawnAt = now.AddSeconds(turret.MinIntervalSeconds + (turret.MaxIntervalSeconds - turret.MinIntervalSeconds) * _random()) };

    /// <summary>Whether a shot (its middle at <paramref name="shot"/>, <paramref name="shotSize"/> across) touches the square of the turret. Touching counts.</summary>
    public static bool TurretIsHit(double x, double y, double size, Point shot, double shotSize)
        => Math.Abs(shot.X - x) <= (size + shotSize) / 2 && Math.Abs(shot.Y - y) <= (size + shotSize) / 2;

    /// <summary>
    /// Moves the safe zone of a room on. Call as often as the damage needs to be timed (every frame is fine). Whether anyone is in
    /// the room is for the caller to say through <paramref name="players"/>.
    /// </summary>
    public ZoneStep ZoneStep(ZoneState state, DateTimeOffset now, double roomWidth, double roomHeight, int players, WorldEventOptions? forRoom = null)
    {
        var options = forRoom ?? _options;
        var zone = options.Zone;
        if (!options.Enabled || !zone.Enabled) return new ZoneStep(ZoneState.None, ZoneAction.None, 0);
        if (!state.IsActive) return ZoneWaiting(zone, state, now, roomWidth, roomHeight, players);
        if (now >= state.EndsAt) return new ZoneStep(ZoneScheduled(zone, now), ZoneAction.End, 0);
        if (now < state.NextDamageAt) return new ZoneStep(state, ZoneAction.None, 0);

        var next = state with { NextDamageAt = now.AddMilliseconds(zone.DamageIntervalMilliseconds) };
        return new ZoneStep(next, ZoneAction.Damage, RadiusAt(state, now, options));
    }

    private ZoneStep ZoneWaiting(ZoneOptions zone, ZoneState state, DateTimeOffset now, double width, double height, int players)
    {
        if (state.NextAt is not { } due) return new ZoneStep(ZoneScheduled(zone, now), ZoneAction.None, 0);
        if (now < due || players < zone.MinPlayers) return new ZoneStep(state, ZoneAction.None, 0);

        var closing = now.AddSeconds(zone.WarningSeconds);
        var closed = closing.AddSeconds(zone.ShrinkSeconds);
        // Far enough that the zone covers the whole room, across its edges too.
        var cover = Math.Sqrt(width * width / 4 + height * height / 4);
        var begun = new ZoneState(
            null, closing, closed, closed.AddSeconds(zone.HoldSeconds), closing, _random() * width, _random() * height, Math.Max(cover, zone.FinalRadius));
        return new ZoneStep(begun, ZoneAction.Begin, 0);
    }

    private ZoneState ZoneScheduled(ZoneOptions zone, DateTimeOffset now)
        => ZoneState.None with { NextAt = now.AddSeconds(zone.MinIntervalSeconds + (zone.MaxIntervalSeconds - zone.MinIntervalSeconds) * _random()) };

    /// <summary>Which part of its life a zone is in.</summary>
    public ZonePhase PhaseOf(ZoneState state, DateTimeOffset now)
        => now < state.ClosingAt ? ZonePhase.Warning : now < state.ClosedAt ? ZonePhase.Closing : ZonePhase.Holding;

    /// <summary>The moment the current phase gives way to the next (the zone going, for the last).</summary>
    public DateTimeOffset PhaseEnd(ZoneState state, DateTimeOffset now)
        => PhaseOf(state, now) switch
        {
            ZonePhase.Warning => state.ClosingAt!.Value,
            ZonePhase.Closing => state.ClosedAt!.Value,
            _ => state.EndsAt!.Value
        };

    /// <summary>How far from its middle the zone reaches: all of the room while warning, closing evenly, then the final radius.</summary>
    public double RadiusAt(ZoneState state, DateTimeOffset now, WorldEventOptions? forRoom = null)
    {
        var final = (forRoom ?? _options).Zone.FinalRadius;
        return PhaseOf(state, now) switch
        {
            ZonePhase.Warning => state.StartRadius,
            ZonePhase.Closing => state.StartRadius + (final - state.StartRadius) * Math.Clamp((now - state.ClosingAt!.Value) / (state.ClosedAt!.Value - state.ClosingAt!.Value), 0, 1),
            _ => final
        };
    }

    /// <summary>Whether a point is outside a zone, going the short way round the world that wraps. The edge is inside.</summary>
    public static bool IsOutsideZone(Point point, Point center, double radius, double roomWidth, double roomHeight)
    {
        var dx = Wrapped(point.X - center.X, roomWidth);
        var dy = Wrapped(point.Y - center.Y, roomHeight);
        return dx * dx + dy * dy > radius * radius;
    }

    private static double Wrapped(double delta, double size)
        => delta > size / 2 ? delta - size : delta < -size / 2 ? delta + size : delta;

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
