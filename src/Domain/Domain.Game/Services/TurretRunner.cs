using System.Collections.Concurrent;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

/// <summary>
/// Runs the turret boss of a room: when <see cref="WorldEventRules"/> says so, puts it in the game state (the snapshot shows it),
/// has it fire its volleys and takes it away when its time is up. Snakes bring it down in the simulation
/// (<see cref="IGameStateService.ResolveBossHits"/>); this notices that it is gone and schedules the next. Call it from the game
/// loop of the room, every frame, like <see cref="WorldEventRunner"/>.
/// </summary>
public sealed class TurretRunner
{
    private readonly WorldEventRules _rules;
    private readonly IGameStateService _gameStates;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, TurretState> _states = new();
    /// <summary>Rooms where a turret was asked for: it comes whatever the number of players, once.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _asked = new();

    public TurretRunner(WorldEventRules rules, IGameStateService gameStates, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(gameStates);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _gameStates = gameStates;
        _time = time;
    }

    /// <summary>
    /// Puts a turret in the room now, even with fewer players than it asks for. False when it is off in the room, nobody is in it, or
    /// one is already there.
    /// </summary>
    public bool Trigger(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Turret.AppliesTo(room)) return false;
        if (_states.GetValueOrDefault(room.Guid, TurretState.None).IsActive) return false;
        _states[room.Guid] = TurretState.None with { NextSpawnAt = _time.GetUtcNow() };
        _asked[room.Guid] = true;
        return true;
    }

    /// <summary>A room without players, or that the turret is not for, forgets it.</summary>
    public void Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Turret.AppliesTo(room))
        {
            _states.TryRemove(room.Guid, out _);
            _asked.TryRemove(room.Guid, out _);
            gameState.Boss = null;
            return;
        }

        var now = _time.GetUtcNow();
        var state = _states.GetValueOrDefault(room.Guid, TurretState.None);
        if (state.IsActive && gameState.Boss is null) state = _rules.TurretKilled(now, options);
        var players = _asked.ContainsKey(room.Guid) ? int.MaxValue : gameState.Players.Count;
        var step = _rules.TurretStep(state, now, room.Width, room.Height, players, options);
        if (step.Action == TurretAction.Spawn) _asked.TryRemove(room.Guid, out _);
        _states[room.Guid] = step.State;
        Act(gameState, options.Turret, step);
    }

    private void Act(IGameState gameState, TurretOptions turret, TurretStep step)
    {
        switch (step.Action)
        {
            case TurretAction.Spawn:
                gameState.Boss = new WorldBoss
                {
                    Id = Guid.NewGuid(), X = step.State.X, Y = step.State.Y, Size = turret.Size, Hp = turret.HitPoints, MaxHp = turret.HitPoints,
                    Angle = step.State.Angle, Arms = turret.Arms, ShotSpeed = turret.ShotSpeed, RewardMass = turret.RewardMass, FoodsCredit = turret.FoodsCredit,
                    GrantsPerk = string.IsNullOrWhiteSpace(turret.GrantsPerk) ? null : turret.GrantsPerk,
                    Reward = turret.RewardFor(gameState.Room) is { IsEmpty: false } reward ? reward : null
                };
                break;
            case TurretAction.Expire:
                gameState.Boss = null;
                break;
            case TurretAction.Fire:
                _gameStates.FireTurret(gameState, step.Angles);
                if (gameState.Boss is { } boss) boss.Angle = step.State.Angle;
                break;
        }
    }
}
