using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Helpers;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Services;

/// <summary>
/// Puts prizes on the map of a room when they are due and takes them off when nobody claimed them in time. Each kind of prize has
/// its own timing, and there is at most one of each kind on a map.
/// </summary>
public sealed class PrizeSpawner
{
    private readonly PrizeRules _rules;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<(Guid Room, string Kind), PrizeState> _states = new();

    public PrizeSpawner(PrizeRules rules, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _time = time;
    }

    /// <summary>Call about once a second for every room. Does nothing in a room without players.</summary>
    public void Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var roomId = gameState.Room.Guid;
        if (gameState.Players.Count == 0)
        {
            RemovePrizes(gameState, _ => true);
            ForgetStates(roomId, _ => true);
            return;
        }

        var active = _rules.Options.Enabled && !gameState.Room.GameplayOf().PrizesOff
            ? _rules.Definitions.Where(definition => definition.AppliesTo(gameState.Room)).ToArray()
            : [];
        var activeIds = active.Select(definition => definition.Id).ToHashSet();
        // The maze's own prize is the maze runner's to take away, not a kind in the list.
        RemovePrizes(gameState, food => food.PrizeId is null || (food.PrizeId != MazeOptions.PrizeId && !activeIds.Contains(food.PrizeId)));
        ForgetStates(roomId, kind => !activeIds.Contains(kind));
        foreach (var definition in active) Step(gameState, definition);
    }

    /// <summary>
    /// Puts a prize of this kind on the map now, for a script that asked for it. Nothing happens when the kind is unknown, off, not
    /// for this room, or already has a prize on the map. It stays for the kind's lifetime, like any other.
    /// </summary>
    public bool Drop(IGameState gameState, string kind)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var definition = _rules.Definitions.FirstOrDefault(candidate => candidate.Id == kind);
        if (definition is null || !_rules.Options.Enabled || gameState.Room.GameplayOf().PrizesOff || !definition.AppliesTo(gameState.Room) || HasPrize(gameState, kind)) return false;
        AddPrize(gameState, definition);
        _states[(gameState.Room.Guid, kind)] = new PrizeState(null, _time.GetUtcNow().AddSeconds(definition.LifetimeSeconds));
        return true;
    }

    private void Step(IGameState gameState, PrizeDefinition definition)
    {
        var key = (gameState.Room.Guid, definition.Id);
        var state = _states.GetValueOrDefault(key, PrizeState.None);
        var step = _rules.Step(definition, state, _time.GetUtcNow(), HasPrize(gameState, definition.Id), gameState.Players.Count);
        _states[key] = step.State;
        if (step.Action == PrizeAction.Spawn) AddPrize(gameState, definition);
        else if (step.Action == PrizeAction.Expire) RemovePrizes(gameState, food => food.PrizeId == definition.Id);
    }

    private void ForgetStates(Guid roomId, Func<string, bool> forget)
    {
        foreach (var key in _states.Keys.Where(key => key.Room == roomId && forget(key.Kind)).ToArray()) _states.TryRemove(key, out _);
    }

    private static bool HasPrize(IGameState gameState, string kind)
        => gameState.Foods.Values.Any(food => food.Kind == FoodKind.Prize && food.PrizeId == kind);

    private static void RemovePrizes(IGameState gameState, Func<IFood, bool> which)
    {
        var prizes = gameState.Foods.Values.Where(food => food.Kind == FoodKind.Prize && which(food)).Select(food => food.Guid).ToArray();
        foreach (var id in prizes) gameState.Foods.Remove(id);
    }

    private static void AddPrize(IGameState gameState, PrizeDefinition definition)
    {
        var room = gameState.Room;
        var prize = new Food(Guid.NewGuid())
        {
            Position = RandomHelper.RandomPosition(0, room.Width, 0, room.Height),
            Color = new Color(definition.Color, definition.Color),
            Value = definition.Value,
            Kind = FoodKind.Prize,
            FoodsCredit = definition.FoodsCredit,
            PrizeId = definition.Id,
            GrantsPerk = string.IsNullOrWhiteSpace(definition.GrantsPerk) ? null : definition.GrantsPerk,
        };
        gameState.Foods.Add(prize.Guid, prize);
    }
}
