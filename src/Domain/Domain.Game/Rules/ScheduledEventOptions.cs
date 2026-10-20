using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>What every event that is timed by <see cref="EventSchedule"/> is told: how often it comes, how many players it needs, and where it is for.</summary>
public interface IScheduledEvent
{
    bool Enabled { get; }
    int MinIntervalSeconds { get; }
    int MaxIntervalSeconds { get; }
    int MinPlayers { get; }
    bool AppliesTo(IRoom? room);
}

/// <summary>Fruit shower: for a while, fruit keeps falling across the room for anyone to grab. A good thing.</summary>
public sealed record FruitShowerOptions : IScheduledEvent
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one shower (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 180;

    /// <summary>The longest wait, in seconds, to the next shower.</summary>
    public int MaxIntervalSeconds { get; init; } = 420;

    /// <summary>Seconds the shower lasts.</summary>
    public int DurationSeconds { get; init; } = 20;

    /// <summary>Fruits that fall every second, at random spots.</summary>
    public int FruitsPerSecond { get; init; } = 8;

    /// <summary>Players needed in the room before one starts.</summary>
    public int MinPlayers { get; init; } = 1;

    /// <summary>The rooms it falls in. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it does not fall in. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>Bounty: one random living snake is marked wanted for a while. Whoever kills it is paid, and the mark ends when it dies or the time is up.</summary>
public sealed record BountyOptions : IScheduledEvent
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one bounty (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 240;

    /// <summary>The longest wait, in seconds, to the next bounty.</summary>
    public int MaxIntervalSeconds { get; init; } = 600;

    /// <summary>Seconds the snake stays wanted when nobody kills it.</summary>
    public int DurationSeconds { get; init; } = 90;

    /// <summary>Mass the snake that kills it is paid.</summary>
    public long RewardMass { get; init; } = 150;

    /// <summary>How many foods the kill counts for toward the perks (and their tiers) of the snake that does it.</summary>
    public int FoodsCredit { get; init; } = 10;

    /// <summary>Players needed in the room before one is set.</summary>
    public int MinPlayers { get; init; } = 3;

    /// <summary>The rooms it is for. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it is not for. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>
/// Mine field: mines appear on the map, shown from the start and armed after a moment, and stay until a snake touches one. A touch
/// blows it up like a strike: the body segments near it break off.
/// </summary>
public sealed record MineFieldOptions : IScheduledEvent
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one field (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 150;

    /// <summary>The longest wait, in seconds, to the next field.</summary>
    public int MaxIntervalSeconds { get; init; } = 360;

    /// <summary>The fewest mines a field puts on the map.</summary>
    public int MinMines { get; init; } = 6;

    /// <summary>The most mines a field puts on the map.</summary>
    public int MaxMines { get; init; } = 14;

    /// <summary>World units around the mine that its blast reaches.</summary>
    public int Radius { get; init; } = 60;

    /// <summary>How close a head must come to the mine to set it off, in world units.</summary>
    public int TriggerRadius { get; init; } = 20;

    /// <summary>Milliseconds a new mine is shown before it is armed and can be set off.</summary>
    public int ArmMilliseconds { get; init; } = 2000;

    /// <summary>Players needed in the room before a field is laid.</summary>
    public int MinPlayers { get; init; } = 1;

    /// <summary>The rooms it is for. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it is not for. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>Sugar rush: for a while boosting costs no mass, for every snake in the room.</summary>
public sealed record SugarRushOptions : IScheduledEvent
{
    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one rush (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 240;

    /// <summary>The longest wait, in seconds, to the next rush.</summary>
    public int MaxIntervalSeconds { get; init; } = 600;

    /// <summary>Seconds the rush lasts.</summary>
    public int DurationSeconds { get; init; } = 30;

    /// <summary>Players needed in the room before one starts.</summary>
    public int MinPlayers { get; init; } = 1;

    /// <summary>The rooms it is for. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it is not for. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}

/// <summary>
/// Maze: for a few minutes a maze stands in a part of the room, with a big prize at its far end. Snakes race through it. Touching a wall
/// costs mass and never kills; walls stop shots and cannot be broken. Sizes are in head sizes (<c>SnakeConfiguration.HeadSize</c>).
/// </summary>
public sealed record MazeOptions : IScheduledEvent
{
    /// <summary>The prize it puts at the end: its id, which no kind of prize in the prize list may use.</summary>
    public const string PrizeId = "maze";

    public bool Enabled { get; init; } = true;

    /// <summary>The shortest wait, in seconds, from one maze (or the room waking up) to the next.</summary>
    public int MinIntervalSeconds { get; init; } = 420;

    /// <summary>The longest wait, in seconds, to the next maze.</summary>
    public int MaxIntervalSeconds { get; init; } = 900;

    /// <summary>Seconds the maze stands when nobody takes the prize.</summary>
    public int DurationSeconds { get; init; } = 240;

    /// <summary>Cells across. The maze is made smaller when the room cannot hold it.</summary>
    public int Columns { get; init; } = 12;

    /// <summary>Cells down. The maze is made smaller when the room cannot hold it.</summary>
    public int Rows { get; init; } = 8;

    /// <summary>Head sizes across a corridor; a wall is one head size thick. At least <see cref="MazeGenerator.MinCorridor"/>, so a long snake can turn.</summary>
    public int CorridorHeads { get; init; } = 4;

    /// <summary>0 to 100: 0 makes long straight halls, 100 turns at every chance. There are always dead ends and a few loops.</summary>
    public int Twistiness { get; init; } = 60;

    /// <summary>Mass a snake loses when its head runs into a wall (at most once every half second). It never dies of it.</summary>
    public long WallMassLoss { get; init; } = 2;

    /// <summary>Share, in percent, of the cells that get a fruit when the maze rises (the entrance and the prize cell never do). 0: none.</summary>
    public int FoodPercent { get; init; } = 25;

    /// <summary>Players needed in the room before a maze rises.</summary>
    public int MinPlayers { get; init; } = 2;

    /// <summary>Mass the snake that takes the prize gains.</summary>
    public long PrizeMass { get; init; } = 200;

    /// <summary>How many foods taking the prize counts for toward perks.</summary>
    public int PrizeFoodsCredit { get; init; } = 20;

    /// <summary>The id of a perk the snake that takes the prize gets. Empty: none.</summary>
    public string? PrizeGrantsPerk { get; init; }

    /// <summary>How the prize looks: six hex digits such as #ffd23f.</summary>
    public string PrizeColor { get; init; } = "#ffd23f";

    /// <summary>The rooms it is for. Empty: every room.</summary>
    public List<string> Rooms { get; init; } = [];

    /// <summary>Rooms it is not for. Wins over <see cref="Rooms"/>.</summary>
    public List<string> ExcludedRooms { get; init; } = [];

    public bool AppliesTo(IRoom? room) => Enabled && RoomNames.Allows(Rooms, ExcludedRooms, room, offUntilNamed: false);
}
