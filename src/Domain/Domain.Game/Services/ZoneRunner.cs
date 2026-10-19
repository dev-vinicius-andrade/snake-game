using System.Collections.Concurrent;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

/// <summary>
/// Runs the shrinking safe zone of a room: when <see cref="WorldEventRules"/> says so, puts it in the game state (the snapshot shows
/// it, closing a little more every frame) and, while it closes and holds, makes the snakes outside pay. Returns the snakes that
/// died of it, for the game loop to tell and remove, like <see cref="WorldEventRunner"/> does for strikes. Call it from the game
/// loop of the room, every frame.
/// </summary>
public sealed class ZoneRunner
{
    private readonly WorldEventRules _rules;
    private readonly IGameStateService _gameStates;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, ZoneState> _states = new();
    /// <summary>Rooms where a zone was asked for: it comes whatever the number of players, once.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _asked = new();

    public ZoneRunner(WorldEventRules rules, IGameStateService gameStates, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(gameStates);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _gameStates = gameStates;
        _time = time;
    }

    /// <summary>
    /// Starts a zone in the room now, even with fewer players than it asks for. False when it is off in the room, nobody is in it, or
    /// one is already going.
    /// </summary>
    public bool Trigger(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Zone.AppliesTo(room)) return false;
        if (_states.GetValueOrDefault(room.Guid, ZoneState.None).IsActive) return false;
        _states[room.Guid] = ZoneState.None with { NextAt = _time.GetUtcNow() };
        _asked[room.Guid] = true;
        return true;
    }

    /// <summary>A room without players, or that the zone is not for, forgets it.</summary>
    public IReadOnlyList<IPlayer> Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Zone.AppliesTo(room))
        {
            _states.TryRemove(room.Guid, out _);
            _asked.TryRemove(room.Guid, out _);
            gameState.Zone = null;
            return [];
        }

        var now = _time.GetUtcNow();
        var players = _asked.ContainsKey(room.Guid) ? int.MaxValue : gameState.Players.Count;
        var step = _rules.ZoneStep(_states.GetValueOrDefault(room.Guid, ZoneState.None), now, room.Width, room.Height, players, options);
        if (step.Action == ZoneAction.Begin) _asked.TryRemove(room.Guid, out _);
        _states[room.Guid] = step.State;
        var state = step.State;
        gameState.Zone = state.IsActive
            ? new WorldZone(state.X, state.Y, _rules.RadiusAt(state, now, options), options.Zone.FinalRadius, ToPhase(_rules.PhaseOf(state, now)), _rules.PhaseEnd(state, now))
            : null;
        return step.Action == ZoneAction.Damage
            ? _gameStates.ApplyZoneDamage(gameState, state.X, state.Y, step.Radius, options.Zone.DamageMass)
            : [];
    }

    private static WorldZonePhase ToPhase(ZonePhase phase) => phase switch
    {
        ZonePhase.Warning => WorldZonePhase.Warning,
        ZonePhase.Closing => WorldZonePhase.Closing,
        _ => WorldZonePhase.Holding
    };
}
