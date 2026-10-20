using System.Collections.Concurrent;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Services;

/// <summary>
/// Runs an event of a room that comes now and then and lasts a while (or is over at once): when <see cref="EventSchedule"/> says
/// so it begins, every frame it ticks, and when it is over, or the room can no longer have it, it clears what it left in the game
/// state. The fruit shower, the bounty, the mine field and the sugar rush are such events. Call <see cref="Update"/> from the game
/// loop of the room, every frame, like <see cref="WorldEventRunner"/>.
/// </summary>
public abstract class ScheduledEventRunner
{
    private readonly WorldEventRules _rules;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, EventWindow> _states = new();
    /// <summary>Rooms where the event was asked for: it comes whatever the number of players, once.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _asked = new();

    protected ScheduledEventRunner(WorldEventRules rules, EventSchedule schedule, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        Schedule = schedule;
        _time = time;
    }

    protected EventSchedule Schedule { get; }

    protected abstract IScheduledEvent SettingsOf(WorldEventOptions options);

    /// <summary>Seconds it lasts once begun. 0: it is over at once, what it did stays (mines).</summary>
    protected abstract int DurationSeconds(WorldEventOptions options);

    protected abstract void Begin(IGameState gameState, WorldEventOptions options, DateTimeOffset now);

    /// <summary>Every frame while the room can have the event, going on or not.</summary>
    protected virtual void Tick(IGameState gameState, WorldEventOptions options, DateTimeOffset now) { }

    /// <summary>Takes away what the event put in the game state: it is over, off, or the room lost its players.</summary>
    protected abstract void Clear(IGameState gameState);

    /// <summary>Whether an event that is going on ended by itself since the last frame (the wanted snake died).</summary>
    protected virtual bool IsOver(IGameState gameState) => false;

    /// <summary>
    /// Starts the event in the room now, even with fewer players than it asks for. False when it is off in the room, nobody is in it,
    /// or one is already going on.
    /// </summary>
    public bool Trigger(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !SettingsOf(options).AppliesTo(room)) return false;
        if (_states.GetValueOrDefault(room.Guid, EventWindow.None).IsActive) return false;
        _states[room.Guid] = EventWindow.None with { NextAt = _time.GetUtcNow() };
        _asked[room.Guid] = true;
        return true;
    }

    /// <summary>A room without players, or that the event is not for, forgets it.</summary>
    public void Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        var settings = SettingsOf(options);
        if (gameState.Players.Count == 0 || !options.Enabled || !settings.AppliesTo(room))
        {
            _states.TryRemove(room.Guid, out _);
            _asked.TryRemove(room.Guid, out _);
            Clear(gameState);
            return;
        }

        var now = _time.GetUtcNow();
        var state = _states.GetValueOrDefault(room.Guid, EventWindow.None);
        if (state.IsActive && IsOver(gameState))
        {
            state = Schedule.Scheduled(settings, now);
            Clear(gameState);
        }
        var players = _asked.ContainsKey(room.Guid) ? int.MaxValue : gameState.Players.Count;
        var step = Schedule.Step(settings, options.Enabled, state, now, players, DurationSeconds(options));
        _states[room.Guid] = step.State;
        if (step.Action == ScheduleAction.Begin)
        {
            _asked.TryRemove(room.Guid, out _);
            Begin(gameState, options, now);
        }
        else if (step.Action == ScheduleAction.End) Clear(gameState);
        Tick(gameState, options, now);
    }
}
