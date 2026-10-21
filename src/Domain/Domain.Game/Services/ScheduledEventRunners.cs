using System.Collections.Concurrent;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

/// <summary>A fruit shower: for a while, fruit falls at random spots of the room. It reuses the food generator.</summary>
public sealed class FruitShowerRunner(WorldEventRules rules, EventSchedule schedule, IGameStateService gameStates, TimeProvider time)
    : ScheduledEventRunner(rules, schedule, time)
{
    /// <summary>Where the last drop was counted and the part of a fruit not dropped yet, per room.</summary>
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset At, double Carry)> _pouring = new();

    /// <summary>A frame never pours more than this many seconds of fruit, so a stall does not flood the room.</summary>
    private const double MaxCatchUpSeconds = 1;

    protected override IScheduledEvent SettingsOf(WorldEventOptions options) => options.FruitShower;

    protected override int DurationSeconds(WorldEventOptions options) => options.FruitShower.DurationSeconds;

    protected override void Begin(IGameState gameState, WorldEventOptions options, DateTimeOffset now)
    {
        gameState.FruitShowerEndsAt = now.AddSeconds(options.FruitShower.DurationSeconds);
        _pouring[gameState.Room.Guid] = (now, 0);
    }

    protected override void Tick(IGameState gameState, WorldEventOptions options, DateTimeOffset now)
    {
        if (gameState.FruitShowerEndsAt is null || !_pouring.TryGetValue(gameState.Room.Guid, out var pouring)) return;
        var seconds = Math.Clamp((now - pouring.At).TotalSeconds, 0, MaxCatchUpSeconds);
        var owed = pouring.Carry + seconds * options.FruitShower.FruitsPerSecond;
        var fruits = (int)Math.Floor(owed + 1e-9);
        _pouring[gameState.Room.Guid] = (now, Math.Max(0, owed - fruits));
        for (var fruit = 0; fruit < fruits; fruit++) gameStates.GenerateFood(gameState.Room);
    }

    protected override void Clear(IGameState gameState)
    {
        gameState.FruitShowerEndsAt = null;
        _pouring.TryRemove(gameState.Room.Guid, out _);
    }
}

/// <summary>A sugar rush: for a while boosting costs no mass. The game state says until when; the game state service does the rest.</summary>
public sealed class SugarRushRunner(WorldEventRules rules, EventSchedule schedule, TimeProvider time)
    : ScheduledEventRunner(rules, schedule, time)
{
    protected override IScheduledEvent SettingsOf(WorldEventOptions options) => options.SugarRush;

    protected override int DurationSeconds(WorldEventOptions options) => options.SugarRush.DurationSeconds;

    protected override void Begin(IGameState gameState, WorldEventOptions options, DateTimeOffset now)
        => gameState.SugarRushEndsAt = now.AddSeconds(options.SugarRush.DurationSeconds);

    protected override void Clear(IGameState gameState) => gameState.SugarRushEndsAt = null;
}

/// <summary>
/// A bounty: one random living snake is marked wanted. The kill is paid by <see cref="IGameStateService"/> (which takes the mark
/// off); this ends the mark when the snake dies some other way, leaves, or the time is up.
/// </summary>
public sealed class BountyRunner(WorldEventRules rules, EventSchedule schedule, TimeProvider time)
    : ScheduledEventRunner(rules, schedule, time)
{
    protected override IScheduledEvent SettingsOf(WorldEventOptions options) => options.Bounty;

    protected override int DurationSeconds(WorldEventOptions options) => options.Bounty.DurationSeconds;

    protected override void Begin(IGameState gameState, WorldEventOptions options, DateTimeOffset now)
    {
        var living = gameState.Players.Values.Where(player => player.IsAlive).ToArray();
        if (living.Length == 0) return;
        var target = living[Math.Min((int)(Schedule.Next() * living.Length), living.Length - 1)];
        gameState.Bounty = new WorldBounty(target.Guid, now.AddSeconds(options.Bounty.DurationSeconds), options.Bounty.RewardMass, options.Bounty.FoodsCredit,
            options.Bounty.RewardFor(gameState.Room) is { IsEmpty: false } reward ? reward : null);
    }

    protected override bool IsOver(IGameState gameState)
        => gameState.Bounty is not { } bounty
           || !gameState.Players.TryGetValue(bounty.Target, out var target)
           || !target.IsAlive;

    protected override void Clear(IGameState gameState) => gameState.Bounty = null;
}

/// <summary>A mine field: mines are laid at random spots and stay until a snake sets one off, which the game state service sees to every frame.</summary>
public sealed class MineFieldRunner(WorldEventRules rules, EventSchedule schedule, IGameStateService gameStates, TimeProvider time)
    : ScheduledEventRunner(rules, schedule, time)
{
    protected override IScheduledEvent SettingsOf(WorldEventOptions options) => options.Mines;

    protected override int DurationSeconds(WorldEventOptions options) => 0;

    protected override void Begin(IGameState gameState, WorldEventOptions options, DateTimeOffset now)
    {
        var mines = options.Mines;
        var count = Math.Min(mines.MinMines + (int)(Schedule.Next() * (mines.MaxMines - mines.MinMines + 1)), mines.MaxMines);
        count = Math.Min(count, WorldEventRules.MaxActiveMines - gameState.Mines.Count);
        var room = gameState.Room;
        for (var index = 0; index < count; index++)
        {
            var mine = new WorldMine(
                Guid.NewGuid(), Schedule.Next() * room.Width, Schedule.Next() * room.Height, mines.Radius, mines.TriggerRadius,
                now.AddMilliseconds(mines.ArmMilliseconds));
            gameState.Mines[mine.Id] = mine;
        }
    }

    protected override void Tick(IGameState gameState, WorldEventOptions options, DateTimeOffset now) => gameStates.ResolveMines(gameState);

    protected override void Clear(IGameState gameState) => gameState.Mines.Clear();
}
