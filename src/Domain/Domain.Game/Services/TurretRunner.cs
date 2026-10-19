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

    public TurretRunner(WorldEventRules rules, IGameStateService gameStates, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(gameStates);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _gameStates = gameStates;
        _time = time;
    }

    /// <summary>A room without players, or that the turret is not for, forgets it.</summary>
    public void Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.Options;
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Turret.AppliesTo(room))
        {
            _states.TryRemove(room.Guid, out _);
            gameState.Boss = null;
            return;
        }

        var now = _time.GetUtcNow();
        var state = _states.GetValueOrDefault(room.Guid, TurretState.None);
        if (state.IsActive && gameState.Boss is null) state = _rules.TurretKilled(now);
        var step = _rules.TurretStep(state, now, room.Width, room.Height, gameState.Players.Count);
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
                    Angle = step.State.Angle, Arms = turret.Arms, ShotSpeed = turret.ShotSpeed, RewardMass = turret.RewardMass,
                    GrantsPerk = string.IsNullOrWhiteSpace(turret.GrantsPerk) ? null : turret.GrantsPerk
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
