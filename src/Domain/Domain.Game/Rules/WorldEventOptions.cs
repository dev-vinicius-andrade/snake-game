using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>The kinds of world event that hit spots of the map. They share one engine, see <see cref="WorldEventRules"/>.</summary>
public enum WorldEventKind
{
    Lightning,
    Meteor
}

/// <summary>Tunable world events: things that happen to a whole room now and then. See docs/WORLD_EVENTS.md.</summary>
public sealed record WorldEventOptions
{
    public const string SectionName = "WorldEventConfiguration";

    /// <summary>Turns every world event off with one switch.</summary>
    public bool Enabled { get; init; } = true;

    public LightningOptions Lightning { get; init; } = new();

    public MeteorOptions Meteors { get; init; } = new();

    public TurretOptions Turret { get; init; } = new();

    public ZoneOptions Zone { get; init; } = new();

    /// <summary>The settings of one kind of event.</summary>
    public IStormSettings Of(WorldEventKind kind) => kind == WorldEventKind.Lightning ? Lightning : Meteors;

    /// <summary>
    /// Fills in the storm of lightning that an older config did not have, so an editor shows it. Without this, lightning from an
    /// older config stays a storm of one strike, as it always was.
    /// </summary>
    public WorldEventOptions WithStormDefaults() => this with
    {
        Lightning = Lightning with
        {
            MinStrikes = Lightning.MinStrikes ?? LightningOptions.DefaultMinStrikes,
            MaxStrikes = Lightning.MaxStrikes ?? LightningOptions.DefaultMaxStrikes,
            StormSeconds = Lightning.StormSeconds ?? LightningOptions.DefaultStormSeconds,
        }
    };
}

/// <summary>
/// What every event that comes as a burst of strikes is told: how often the burst comes, how many strikes it has over how
/// long, how big and how well signalled each is, and where it is for.
/// </summary>
public interface IStormSettings
{
    bool Enabled { get; }
    int MinIntervalSeconds { get; }
    int MaxIntervalSeconds { get; }
    int Radius { get; }
    int TelegraphMilliseconds { get; }
    int MinCount { get; }
    int MaxCount { get; }
    /// <summary>Seconds over which the strikes of one burst begin.</summary>
    int DurationSeconds { get; }
    /// <summary>Share (0..1) of strikes that land near a random living snake instead of anywhere.</summary>
    double AimedShare { get; }
    IReadOnlyList<string> Rooms { get; }
    IReadOnlyList<string> ExcludedRooms { get; }
    bool AppliesTo(IRoom? room);
}

/// <summary>Lightning storms: a burst of small strikes, each marked on the map a moment before it falls.</summary>
public sealed record LightningOptions : IStormSettings
{
    public const int DefaultMinStrikes = 5;
    public const int DefaultMaxStrikes = 12;
    public const int DefaultStormSeconds = 6;

    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one storm (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 45;

    /// <summary>The longest wait, in seconds, to the next storm.</summary>
    public int MaxIntervalSeconds { get; init; } = 120;

    /// <summary>World units around the spot that a strike reaches.</summary>
    public int Radius { get; init; } = 90;

    /// <summary>Milliseconds each spot is shown before it is struck, so snakes can get out of it.</summary>
    public int TelegraphMilliseconds { get; init; } = 1500;

    /// <summary>The fewest strikes in a storm. Not set (configs from before storms): one.</summary>
    public int? MinStrikes { get; init; }

    /// <summary>The most strikes in a storm. Not set: one.</summary>
    public int? MaxStrikes { get; init; }

    /// <summary>Seconds over which the strikes of a storm begin. Not set: one.</summary>
    public int? StormSeconds { get; init; }

    /// <summary>Share (0..1) of strikes aimed near a random living snake. 0: all random.</summary>
    public double AimedShare { get; init; }

    /// <summary>The rooms it strikes in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not strike in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    int IStormSettings.MinCount => MinStrikes ?? 1;
    int IStormSettings.MaxCount => MaxStrikes ?? 1;
    int IStormSettings.DurationSeconds => StormSeconds ?? 1;
    IReadOnlyList<string> IStormSettings.Rooms => Rooms;
    IReadOnlyList<string> IStormSettings.ExcludedRooms => ExcludedRooms;

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>Meteor rain: a shower of meteors, each shown by a big shadow for a while before it lands.</summary>
public sealed record MeteorOptions : IStormSettings
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one rain (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 120;

    /// <summary>The longest wait, in seconds, to the next rain.</summary>
    public int MaxIntervalSeconds { get; init; } = 300;

    /// <summary>World units around the spot that an impact reaches.</summary>
    public int Radius { get; init; } = 130;

    /// <summary>Milliseconds the shadow of each meteor is shown before it lands.</summary>
    public int TelegraphMilliseconds { get; init; } = 2000;

    public int MinMeteors { get; init; } = 6;
    public int MaxMeteors { get; init; } = 14;

    /// <summary>Seconds over which the meteors of a rain begin to fall.</summary>
    public int RainSeconds { get; init; } = 8;

    /// <summary>Share (0..1) of meteors aimed near a random living snake. 0: all random.</summary>
    public double AimedShare { get; init; } = 0.2;

    /// <summary>The rooms it rains in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not rain in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    int IStormSettings.MinCount => MinMeteors;
    int IStormSettings.MaxCount => MaxMeteors;
    int IStormSettings.DurationSeconds => RainSeconds;
    IReadOnlyList<string> IStormSettings.Rooms => Rooms;
    IReadOnlyList<string> IStormSettings.ExcludedRooms => ExcludedRooms;

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>
/// The turret: a mini boss that now and then appears in a room, sits still and fires volleys that turn round it, until snakes shoot
/// it down for a reward or its time is up.
/// </summary>
public sealed record TurretOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one turret going (or the room waking up) to the next appearing.</summary>
    public int MinIntervalSeconds { get; init; } = 180;

    /// <summary>The longest wait, in seconds, to the next turret.</summary>
    public int MaxIntervalSeconds { get; init; } = 420;

    /// <summary>Seconds it stays when nobody brings it down.</summary>
    public int LifetimeSeconds { get; init; } = 90;

    /// <summary>Shots it takes to bring it down.</summary>
    public int HitPoints { get; init; } = 40;

    /// <summary>Side of its square, in world units. A snake segment is 15.</summary>
    public int Size { get; init; } = 60;

    /// <summary>Milliseconds between two volleys.</summary>
    public int FireIntervalMilliseconds { get; init; } = 900;

    /// <summary>Shots in a volley, evenly spread round it.</summary>
    public int Arms { get; init; } = 6;

    /// <summary>Degrees the volley turns by every time it fires.</summary>
    public double SpinDegrees { get; init; } = 11;

    /// <summary>How fast its shots fly compared with a snake shot (1). Slower can be dodged.</summary>
    public double ShotSpeed { get; init; } = 0.5;

    /// <summary>Players needed in the room before one appears; alone there is nobody to fight it with.</summary>
    public int MinPlayers { get; init; } = 2;

    /// <summary>Mass that falls as food where it is brought down.</summary>
    public long RewardMass { get; init; } = 60;

    /// <summary>The id of a perk the snake that brings it down gets, whatever the foods it has eaten. Empty: none.</summary>
    public string? GrantsPerk { get; init; }

    /// <summary>How many foods bringing it down counts for toward the perks (and their tiers) of the snake that does it.</summary>
    public int FoodsCredit { get; init; } = 15;

    /// <summary>The rooms it appears in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not appear in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>
/// The shrinking safe zone: a circle on the map, shown first and then closing in. A snake whose head is outside it loses mass
/// every moment, and dies when it has none left. It stays at its smallest for a while and then goes.
/// </summary>
public sealed record ZoneOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one zone going (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 300;

    /// <summary>The longest wait, in seconds, to the next zone.</summary>
    public int MaxIntervalSeconds { get; init; } = 600;

    /// <summary>Seconds the safe spot is shown before the zone begins to close, with nobody harmed.</summary>
    public int WarningSeconds { get; init; } = 15;

    /// <summary>Seconds it takes to close from covering the whole room to its smallest.</summary>
    public int ShrinkSeconds { get; init; } = 60;

    /// <summary>Seconds it stays at its smallest before it goes.</summary>
    public int HoldSeconds { get; init; } = 30;

    /// <summary>World units around the safe spot that stay safe at the end.</summary>
    public int FinalRadius { get; init; } = 160;

    /// <summary>Milliseconds between two times a snake outside is made to pay.</summary>
    public int DamageIntervalMilliseconds { get; init; } = 1000;

    /// <summary>Mass a snake outside loses each time. It falls as food where the snake is. A snake with none left dies.</summary>
    public int DamageMass { get; init; } = 4;

    /// <summary>Players needed in the room before one appears.</summary>
    public int MinPlayers { get; init; } = 2;

    /// <summary>The rooms it is for. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it is not for. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}
