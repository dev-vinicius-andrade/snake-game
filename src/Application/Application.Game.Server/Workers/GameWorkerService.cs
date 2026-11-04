using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Game.Server.Hubs;
using Application.Game.Server.Services;
using Domain.Game.Services;
using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Application.Game.Server.Workers;

/// <summary>
/// Schedules fixed simulation steps per room and sends snapshots. Game rules live in <see cref="RoomSimulation"/>.
/// </summary>
public class GameWorkerService : BackgroundService
{
    private const int MaxStepsPerFrame = 5;
    private const int SnapshotEveryTicks = 2;

    private readonly Serilog.ILogger _logger;
    private readonly IHubContext<GameHub, IGameHubSendEvents> _gameHub;
    private readonly IPlayerRoomService _playerRoomService;
    private readonly IGameStateService _gameStateService;
    private readonly ITrackableIdGeneratorService _trackableIdGeneratorService;
    private readonly ITickRateService _tickRateService;
    private readonly RoomSimulation _roomSimulation;
    private readonly IRoomInputQueue _roomInputQueue;
    private readonly WorldEventRunner _worldEvents;
    private readonly TurretRunner _turrets;
    private readonly ZoneRunner _zones;
    private readonly FruitShowerRunner _fruitShowers;
    private readonly BountyRunner _bounties;
    private readonly MineFieldRunner _mines;
    private readonly SugarRushRunner _sugarRushes;
    private readonly MazeRunner _mazes;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, FixedTimestepClock> _clocks = new();
    /// <summary>What each room last heard as its ranking, and what each player last heard as their score: said again only when it changes.</summary>
    private readonly ConcurrentDictionary<Guid, string> _lastRanking = new();
    private readonly ConcurrentDictionary<string, long> _lastScores = new();
    /// <summary>A snapshot goes to each player on its own, so a player on a slow link skips ahead instead of holding the room (and itself) back.</summary>
    private readonly LatestOnlySender<SnapshotEvent> _snapshots;
    /// <summary>The score and the ranking go the same way: a player behind only needs the newest.</summary>
    private readonly LatestOnlySender<long> _scores;
    /// <summary>The foods are not in every snapshot: a player is sent them when they differ from the ones it last heard of.</summary>
    private readonly LatestOnlySender<FoodsEvent> _foods;
    private readonly ConcurrentDictionary<string, int> _foodsHeard = new();
    private readonly LatestOnlySender<IList<IScore>> _rankings;

    public GameWorkerService(
        Serilog.ILogger logger,
        IHubContext<GameHub, IGameHubSendEvents> gameHub,
        IPlayerRoomService playerRoomService,
        IGameStateService gameStateService,
        ITrackableIdGeneratorService trackableIdGeneratorService,
        ITickRateService tickRateService,
        RoomSimulation roomSimulation,
        IRoomInputQueue roomInputQueue,
        WorldEventRunner worldEvents,
        TurretRunner turrets,
        ZoneRunner zones,
        FruitShowerRunner fruitShowers,
        BountyRunner bounties,
        MineFieldRunner mines,
        SugarRushRunner sugarRushes,
        MazeRunner mazes,
        TimeProvider time)
    {
        _snapshots = new LatestOnlySender<SnapshotEvent>(
            (connectionId, snapshot) => gameHub.Clients.Client(connectionId).Snapshot(snapshot),
            (connectionId, failure) => logger.Warning(failure, "Could not send a snapshot to {ConnectionId}", connectionId));
        _scores = new LatestOnlySender<long>(
            (connectionId, score) => gameHub.Clients.Client(connectionId).PlayerScoreUpdated(score),
            (connectionId, failure) => logger.Warning(failure, "Could not send a score to {ConnectionId}", connectionId));
        _foods = new LatestOnlySender<FoodsEvent>(
            (connectionId, foods) => gameHub.Clients.Client(connectionId).FoodsUpdated(foods),
            (connectionId, failure) => logger.Warning(failure, "Could not send the foods to {ConnectionId}", connectionId));
        _rankings = new LatestOnlySender<IList<IScore>>(
            (connectionId, ranking) => gameHub.Clients.Client(connectionId).ScoreUpdated(ranking),
            (connectionId, failure) => logger.Warning(failure, "Could not send the ranking to {ConnectionId}", connectionId));
        _fruitShowers = fruitShowers;
        _bounties = bounties;
        _mines = mines;
        _sugarRushes = sugarRushes;
        _mazes = mazes;
        _worldEvents = worldEvents;
        _turrets = turrets;
        _zones = zones;
        _time = time;
        _roomInputQueue = roomInputQueue;
        _logger = logger;
        _gameHub = gameHub;
        _playerRoomService = playerRoomService;
        _gameStateService = gameStateService;
        _trackableIdGeneratorService = trackableIdGeneratorService;
        _tickRateService = tickRateService;
        _roomSimulation = roomSimulation;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_tickRateService.TickRate / 2);
        var stopwatch = Stopwatch.StartNew();
        var previous = TimeSpan.Zero;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = stopwatch.Elapsed;
            await RunFrameAsync(now - previous, stoppingToken);
            previous = now;
        }
    }

    private async Task RunFrameAsync(TimeSpan elapsed, CancellationToken cancellationToken)
    {
        var roomsWithPlayers = _playerRoomService.GetRoomsPlayers().Keys;
        var roomsWithPendingInput = _roomInputQueue.RoomsWithPendingCommands().ToHashSet();
        var activeRooms = roomsWithPlayers.Union(roomsWithPendingInput).ToArray();
        DiscardClocksOfClosedRooms(activeRooms);
        await Parallel.ForEachAsync(activeRooms, cancellationToken,
            (roomId, token) => ProcessRoomAsync(roomId, roomsWithPendingInput.Contains(roomId), elapsed, token));
    }

    private async ValueTask ProcessRoomAsync(Guid roomId, bool hasPendingInput, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        try
        {
            var gameState = _gameStateService.GetGameState(_trackableIdGeneratorService.Generate(roomId));
            if (gameState is null || (gameState.Players.Count == 0 && !hasPendingInput)) return;
            var ticks = _clocks.GetOrAdd(roomId, CreateClock).Advance(elapsed);
            if (ticks.Count == 0) return;
            // Every frame, not once a second: the lightning warning is timed in fractions of a second, and the snakes it
            // kills are told and removed below, with the ones the simulation killed.
            var struck = _worldEvents.Update(gameState).Concat(_zones.Update(gameState)).ToList();
            _turrets.Update(gameState);
            _fruitShowers.Update(gameState);
            _bounties.Update(gameState);
            _mines.Update(gameState);
            _sugarRushes.Update(gameState);
            _mazes.Update(gameState);
            var diedPlayers = (await StepAsync(gameState, ticks, cancellationToken)).Concat(struck).ToList();
            NotifyDamage(gameState);
            NotifyDied(diedPlayers);
            if (diedPlayers.Count > 0 || ticks.Any(tick => tick.BroadcastSnapshot))
                await BroadcastSnapshotAsync(roomId, gameState);
            await RemoveDiedAsync(gameState, diedPlayers);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Error processing room {RoomId}", roomId);
        }
    }

    private async Task<IReadOnlyList<IPlayer>> StepAsync(
        IGameState gameState, IReadOnlyList<SimulationTick> ticks, CancellationToken cancellationToken)
    {
        var diedPlayers = new List<IPlayer>();
        foreach (var _ in ticks)
            diedPlayers.AddRange((await _roomSimulation.StepAsync(gameState, cancellationToken)).DiedPlayers);
        return diedPlayers;
    }

    private void NotifyDamage(IGameState gameState)
    {
        foreach (var notice in DamageNotices.Drain(gameState))
            Detached.Run(() => _gameHub.Clients.Client(notice.ConnectionId).DamageDealt(notice.Event), FailedToTell(notice.ConnectionId));
    }

    private Action<Exception> FailedToTell(string connectionId)
        => failure => _logger.Warning(failure, "Could not send an event to {ConnectionId}", connectionId);

    private void NotifyDied(IEnumerable<IPlayer> diedPlayers)
    {
        foreach (var player in diedPlayers)
            Detached.Run(() => _gameHub.Clients.Client(player.Id).PlayerDied(new PlayerDiedEvent { Score = player.Score }), FailedToTell(player.Id));
    }

    private async Task BroadcastSnapshotAsync(Guid roomId, IGameState gameState)
    {
        await SendRankingAsync(roomId, gameState);
        SendPlayerScores(gameState);
        var snapshot = SnapshotBuilder.Build(gameState, _time.GetUtcNow(), includeFoods: false);
        var players = gameState.Players.Values.ToArray();
        SendFoodsThatChanged(gameState, players);
        foreach (var player in players) _snapshots.Send(player.Id, snapshot);
    }

    private void SendFoodsThatChanged(IGameState gameState, IReadOnlyList<IPlayer> players)
    {
        var signature = SnapshotBuilder.FoodsSignature(gameState);
        var behind = players.Where(player => !_foodsHeard.TryGetValue(player.Id, out var heard) || heard != signature).ToArray();
        if (behind.Length == 0) return;
        var foods = SnapshotBuilder.BuildFoods(gameState);
        foreach (var player in behind)
        {
            _foodsHeard[player.Id] = signature;
            _foods.Send(player.Id, foods);
        }
    }

    private async Task SendRankingAsync(Guid roomId, IGameState gameState)
    {
        var ranking = await _gameStateService.GetScore(gameState);
        var signature = string.Join('|', ranking.Select(entry => $"{entry.Nickname}:{entry.CurrentScore}"));
        if (_lastRanking.TryGetValue(roomId, out var last) && last == signature) return;
        _lastRanking[roomId] = signature;
        foreach (var player in gameState.Players.Values.ToArray()) _rankings.Send(player.Id, ranking);
    }

    private void SendPlayerScores(IGameState gameState)
    {
        foreach (var player in gameState.Players.Values.ToArray())
        {
            if (_lastScores.TryGetValue(player.Id, out var last) && last == player.Score) continue;
            _lastScores[player.Id] = player.Score;
            _scores.Send(player.Id, player.Score);
        }
    }

    /// <summary>
    /// Removes the snakes that died, after they were told. Removing ends the life: the stats of a signed-in one are reported then, once
    /// (GameStateService.DisconnectPlayer).
    /// </summary>
    private async Task RemoveDiedAsync(IGameState gameState, IEnumerable<IPlayer> diedPlayers)
    {
        foreach (var player in diedPlayers)
        {
            _lastScores.TryRemove(player.Id, out _);
            _foodsHeard.TryRemove(player.Id, out _);
            await _gameStateService.DisconnectPlayer(gameState.Room, player);
        }
    }

    private void DiscardClocksOfClosedRooms(IEnumerable<Guid> openRoomIds)
    {
        var open = openRoomIds.ToHashSet();
        foreach (var closedRoomId in _clocks.Keys.Where(id => !open.Contains(id)))
        {
            _clocks.TryRemove(closedRoomId, out _);
            _lastRanking.TryRemove(closedRoomId, out _);
        }
    }

    private FixedTimestepClock CreateClock(Guid roomId)
        => new(_tickRateService.TickRate, MaxStepsPerFrame, SnapshotEveryTicks);
}
