using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Game.Server.Hubs;
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
    private readonly ConcurrentDictionary<Guid, FixedTimestepClock> _clocks = new();

    public GameWorkerService(
        Serilog.ILogger logger,
        IHubContext<GameHub, IGameHubSendEvents> gameHub,
        IPlayerRoomService playerRoomService,
        IGameStateService gameStateService,
        ITrackableIdGeneratorService trackableIdGeneratorService,
        ITickRateService tickRateService,
        RoomSimulation roomSimulation,
        IRoomInputQueue roomInputQueue)
    {
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
            var diedPlayers = await StepAsync(gameState, ticks, cancellationToken);
            await NotifyDiedAsync(diedPlayers);
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

    private async Task NotifyDiedAsync(IEnumerable<IPlayer> diedPlayers)
    {
        foreach (var player in diedPlayers)
            await _gameHub.Clients.Client(player.Id).PlayerDied(new PlayerDiedEvent { Score = player.Score });
    }

    private async Task BroadcastSnapshotAsync(Guid roomId, IGameState gameState)
    {
        var group = _gameHub.Clients.Group(roomId.ToString());
        await group.ScoreUpdated(await _gameStateService.GetScore(gameState));
        await SendPlayerScoresAsync(gameState);
        await group.GameStateUpdated(gameState);
    }

    private async Task SendPlayerScoresAsync(IGameState gameState)
    {
        foreach (var player in gameState.Players.Values.ToArray())
            await _gameHub.Clients.Client(player.Id).PlayerScoreUpdated(player.Score);
    }

    private async Task RemoveDiedAsync(IGameState gameState, IEnumerable<IPlayer> diedPlayers)
    {
        foreach (var player in diedPlayers)
            await _gameStateService.DisconnectPlayer(gameState.Room, player);
    }

    private void DiscardClocksOfClosedRooms(IEnumerable<Guid> openRoomIds)
    {
        var open = openRoomIds.ToHashSet();
        foreach (var closedRoomId in _clocks.Keys.Where(id => !open.Contains(id)))
            _clocks.TryRemove(closedRoomId, out _);
    }

    private FixedTimestepClock CreateClock(Guid roomId)
        => new(_tickRateService.TickRate, MaxStepsPerFrame, SnapshotEveryTicks);
}
