using System.Collections.Concurrent;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

/// <summary>
/// Carries out the world events of a room: when <see cref="WorldEventRules"/> says so, marks a spot in the game state (the snapshot
/// shows it) and strikes it when its warning is over. Lightning storms and meteor rains are run alike. Call it from the game loop
/// of the room, every frame: the warnings are timed in fractions of a second, and the snakes a strike kills are to be removed by
/// that same loop.
/// </summary>
public sealed class WorldEventRunner
{
    /// <summary>The most spots marked at once in a room, whatever is going on, so a snapshot stays small.</summary>
    public const int MaxActiveStrikes = 40;

    private readonly WorldEventRules _rules;
    private readonly IGameStateService _gameStates;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<(Guid Room, WorldEventKind Kind), Storm> _storms = new();

    /// <summary>One kind of event in one room: where its storm is, and the ids of the spots it has marked.</summary>
    private sealed class Storm
    {
        public StormState State { get; set; } = StormState.None;
        public List<(StormStrike Strike, Guid Id)> Marked { get; } = [];
    }

    public WorldEventRunner(WorldEventRules rules, IGameStateService gameStates, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(gameStates);
        ArgumentNullException.ThrowIfNull(time);
        _rules = rules;
        _gameStates = gameStates;
        _time = time;
    }

    /// <summary>Returns the snakes the strikes killed this time, to be told and removed. A room without players, or that an event is not for, forgets it.</summary>
    public IReadOnlyList<IPlayer> Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var died = new List<IPlayer>();
        foreach (var kind in Enum.GetValues<WorldEventKind>()) died.AddRange(Run(gameState, kind));
        return died;
    }

    /// <summary>
    /// Starts a storm of this kind in the room now, without waiting for the interval. False when the event is off in the room, nobody
    /// is in it, or a storm of the kind is already going on.
    /// </summary>
    public bool Trigger(IGameState gameState, WorldEventKind kind)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Of(kind).AppliesTo(room)) return false;
        var storm = _storms.GetOrAdd((room.Guid, kind), _ => new Storm());
        if (storm.State.InProgress) return false;
        storm.State = new StormState(_time.GetUtcNow(), [], []);
        return true;
    }

    private IReadOnlyList<IPlayer> Run(IGameState gameState, WorldEventKind kind)
    {
        var room = gameState.Room;
        var options = _rules.OptionsFor(room);
        if (gameState.Players.Count == 0 || !options.Enabled || !options.Of(kind).AppliesTo(room))
        {
            Forget(gameState, kind);
            return [];
        }

        var storm = _storms.GetOrAdd((room.Guid, kind), _ => new Storm());
        var heads = gameState.Players.Values.Where(player => player.IsAlive).Select(player => new Point(player.CurrentPosition.X, player.CurrentPosition.Y)).ToArray();
        var step = _rules.Step(kind, storm.State, _time.GetUtcNow(), room.Width, room.Height, heads, options);
        storm.State = step.State;
        foreach (var strike in step.Started) Mark(gameState, storm, strike, kind);
        return step.Fired.SelectMany(strike => Strike(gameState, storm, strike)).ToList();
    }

    private static void Mark(IGameState gameState, Storm storm, StormStrike strike, WorldEventKind kind)
    {
        // Past the cap the strike is dropped: it is never shown, so it never falls.
        if (gameState.Strikes.Count >= MaxActiveStrikes) return;
        var id = Guid.NewGuid();
        storm.Marked.Add((strike, id));
        gameState.Strikes[id] = new WorldStrike(strike.X, strike.Y, strike.Radius, strike.StrikesAt, ToStrikeKind(kind));
    }

    private IReadOnlyList<IPlayer> Strike(IGameState gameState, Storm storm, StormStrike strike)
    {
        var index = storm.Marked.FindIndex(marked => marked.Strike == strike);
        if (index < 0) return [];
        gameState.Strikes.TryRemove(storm.Marked[index].Id, out _);
        storm.Marked.RemoveAt(index);
        return _gameStates.StrikeArea(gameState, strike.X, strike.Y, strike.Radius);
    }

    private void Forget(IGameState gameState, WorldEventKind kind)
    {
        _storms.TryRemove((gameState.Room.Guid, kind), out _);
        var strikeKind = ToStrikeKind(kind);
        foreach (var (id, _) in gameState.Strikes.Where(entry => entry.Value.Kind == strikeKind).ToArray()) gameState.Strikes.TryRemove(id, out _);
    }

    private static WorldStrikeKind ToStrikeKind(WorldEventKind kind)
        => kind == WorldEventKind.Meteor ? WorldStrikeKind.Meteor : WorldStrikeKind.Lightning;
}
