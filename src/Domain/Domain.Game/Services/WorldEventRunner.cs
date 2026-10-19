using System.Collections.Concurrent;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

/// <summary>
/// Carries out the world events of a room: when <see cref="WorldEventRules"/> says so, marks a spot for lightning in the game state
/// (the snapshot shows it) and strikes it when the warning is over. Call it from the game loop of the room, every frame: the warning
/// is timed in fractions of a second, and the snakes a strike kills are to be removed by that same loop.
/// </summary>
public sealed class WorldEventRunner
{
    private readonly WorldEventRules _rules;
    private readonly IGameStateService _gameStates;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, (LightningState State, Guid StrikeId)> _rooms = new();

    public WorldEventRunner(WorldEventRules rules, IGameStateService gameStates, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(gameStates);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _gameStates = gameStates;
        _time = time;
    }

    /// <summary>Returns the snakes a strike killed this time, to be told and removed. A room without players, or that is not struck, forgets its events.</summary>
    public IReadOnlyList<IPlayer> Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        if (gameState.Players.Count == 0 || !_rules.Options.Lightning.AppliesTo(room))
        {
            Forget(gameState);
            return [];
        }

        var (state, strikeId) = _rooms.GetValueOrDefault(room.Guid);
        var step = _rules.Step(state, _time.GetUtcNow(), room.Width, room.Height);
        if (step.Action == LightningAction.None && step.State == LightningState.None)
        {
            Forget(gameState);
            return [];
        }

        var died = Act(gameState, step, ref strikeId);
        _rooms[room.Guid] = (step.State, strikeId);
        return died;
    }

    private IReadOnlyList<IPlayer> Act(IGameState gameState, LightningStep step, ref Guid strikeId)
    {
        if (step.Action == LightningAction.Telegraph)
        {
            strikeId = Guid.NewGuid();
            var spot = step.Strike!.Value;
            gameState.Strikes[strikeId] = new WorldStrike(spot.X, spot.Y, spot.Radius, spot.StrikesAt);
        }
        else if (step.Action == LightningAction.Strike)
        {
            gameState.Strikes.TryRemove(strikeId, out _);
            var spot = step.Strike!.Value;
            return _gameStates.StrikeArea(gameState, spot.X, spot.Y, spot.Radius);
        }
        return [];
    }

    private void Forget(IGameState gameState)
    {
        _rooms.TryRemove(gameState.Room.Guid, out _);
        gameState.Strikes.Clear();
    }
}
