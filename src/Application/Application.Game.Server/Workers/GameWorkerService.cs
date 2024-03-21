using Application.Game.Server.Entities.Configurations;
using Application.Game.Server.Hubs;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Application.Game.Server.Workers;

public class GameWorkerService : BackgroundService
{
    private readonly Serilog.ILogger _logger;
    private readonly IOptions<AppSettings> _appSettings;
    private  readonly IOptions<ServerConfiguration> _serverConfiguration;
    private readonly IHubContext<GameHub, IGameHubSendEvents> _gameHub;
    private readonly IPlayerRoomService _playerRoomService;
    private readonly IGameStateService _gameStateService;
    private readonly ITrackableIdGeneratorService _trackableIdGeneratorService;
    
    private readonly ITickRateService _tickRateService;

    public GameWorkerService(
        Serilog.ILogger logger,
        IOptions<AppSettings> appSettings,
        IHubContext<GameHub, IGameHubSendEvents> gameHub, 
        IPlayerRoomService playerRoomService, 
        IGameStateService gameStateService, 
        ITrackableIdGeneratorService trackableIdGeneratorService,
        IOptions<ServerConfiguration> serverConfiguration,
        ITickRateService tickRateService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _gameHub = gameHub;
        _playerRoomService = playerRoomService;
        _gameStateService = gameStateService;
        _trackableIdGeneratorService = trackableIdGeneratorService;
        _serverConfiguration = serverConfiguration;
        _tickRateService = tickRateService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            var rooms = _playerRoomService.GetRoomsPlayers();
            await Parallel.ForEachAsync(rooms, stoppingToken, ProcessRooms);
            await Task.Delay(_tickRateService.TickRate, stoppingToken);
        }

    }

    private async ValueTask ProcessRooms(KeyValuePair<Guid, IList<IPlayer>> room, CancellationToken cancellationToken = default)
    {
        var gameState = _gameStateService.GetGameState(_trackableIdGeneratorService.Generate(room.Key));
        if (gameState == null || gameState.Players.Count == 0) return;
        var playerTasks = gameState.Players.Values.Select(player => Task.Run(async () =>
        {
            try
            {
                await _gameStateService.MoveObstacles(gameState);
                await _gameStateService.MovePlayer(gameState, player);
                await _gameStateService.CheckFoodCollision(gameState, player);
                await _gameStateService.CheckObstacleCollision(gameState, player);
                var score = await _gameStateService.GetScore(gameState);
                await _gameHub.Clients.Group(room.Key.ToString()).ScoreUpdated(score);
                await _gameHub.Clients.Client(player.Id).PlayerScoreUpdated(player.Score);
                if(!player.IsAlive)
                    await _gameHub.Clients.Client(player.Id).PlayerDied(new PlayerDiedEvent
                    {
                        Score = player.Score
                    });
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error processing player movement");
            }


        }, cancellationToken)).ToList();
        await Task.WhenAll(playerTasks);
        await _gameHub.Clients.Group(room.Key.ToString()).GameStateUpdated(gameState);
    }

}