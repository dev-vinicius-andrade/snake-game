using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Helpers;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Services;

/// <summary>Puts the prize on the map of a room when it is due and takes it off when nobody claimed it in time.</summary>
public sealed class PrizeSpawner
{
    private const string PrizeColor = "#ffd23f";

    private readonly PrizeRules _rules;
    private readonly PrizeOptions _options;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, PrizeState> _states = new();

    public PrizeSpawner(PrizeRules rules, PrizeOptions options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _options = options;
        _time = time;
    }

    /// <summary>Call about once a second for every room. Does nothing in a room without players.</summary>
    public void Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var roomId = gameState.Room.Guid;
        if (gameState.Players.Count == 0)
        {
            RemovePrizes(gameState);
            _states.TryRemove(roomId, out _);
            return;
        }

        var state = _states.GetValueOrDefault(roomId, PrizeState.None);
        var step = _rules.Step(state, _time.GetUtcNow(), HasPrize(gameState), gameState.Players.Count);
        _states[roomId] = step.State;
        Apply(gameState, step.Action);
    }

    private void Apply(IGameState gameState, PrizeAction action)
    {
        if (action == PrizeAction.Spawn) AddPrize(gameState);
        else if (action == PrizeAction.Expire) RemovePrizes(gameState);
    }

    private static bool HasPrize(IGameState gameState)
        => gameState.Foods.Values.Any(food => food.Kind == FoodKind.Prize);

    private static void RemovePrizes(IGameState gameState)
    {
        var prizes = gameState.Foods.Values.Where(food => food.Kind == FoodKind.Prize).Select(food => food.Guid).ToArray();
        foreach (var id in prizes) gameState.Foods.Remove(id);
    }

    private void AddPrize(IGameState gameState)
    {
        var room = gameState.Room;
        var prize = new Food(Guid.NewGuid())
        {
            Position = RandomHelper.RandomPosition(0, room.Width, 0, room.Height),
            Color = new Color(PrizeColor, PrizeColor),
            Value = _options.Value,
            Kind = FoodKind.Prize,
            FoodsCredit = _options.FoodsCredit
        };
        gameState.Foods.Add(prize.Guid, prize);
    }
}
