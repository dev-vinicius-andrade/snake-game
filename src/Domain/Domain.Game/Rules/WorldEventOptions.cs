using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>Tunable world events: things that happen to a whole room now and then. See docs/WORLD_EVENTS.md.</summary>
public sealed record WorldEventOptions
{
    /// <summary>Turns every world event off with one switch.</summary>
    public bool Enabled { get; init; } = true;

    public LightningOptions Lightning { get; init; } = new();
}

/// <summary>Lightning: at random moments a spot of the map is marked, and a moment later it is struck.</summary>
public sealed record LightningOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one strike (or the room waking up) to the next warning.</summary>
    public int MinIntervalSeconds { get; init; } = 30;

    /// <summary>The longest wait, in seconds, to the next warning.</summary>
    public int MaxIntervalSeconds { get; init; } = 90;

    /// <summary>World units around the spot that the strike reaches.</summary>
    public int Radius { get; init; } = 90;

    /// <summary>Milliseconds the spot is shown before the strike, so snakes can get out of it.</summary>
    public int TelegraphMilliseconds { get; init; } = 1500;

    /// <summary>The rooms it strikes in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not strike in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}
