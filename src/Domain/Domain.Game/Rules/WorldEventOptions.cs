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
