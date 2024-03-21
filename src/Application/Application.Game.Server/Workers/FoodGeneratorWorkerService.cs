using Application.Game.Server.Entities.Configurations;
using Application.Game.Server.Hubs;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Application.Game.Server.Workers;

public class FoodGeneratorWorkerService : BackgroundService
{
    private readonly Serilog.ILogger _logger;
    private readonly IOptions<AppSettings> _appSettings;
    private readonly IHubContext<GameHub, IGameHubSendEvents> _gameHub;
    private readonly IPlayerRoomService _playerRoomService;
    private readonly IGameStateService _gameStateService;
    private readonly ITrackableIdGeneratorService _trackableIdGeneratorService;
    public FoodGeneratorWorkerService(Serilog.ILogger logger, IOptions<AppSettings> appSettings, IHubContext<GameHub, IGameHubSendEvents> gameHub, IPlayerRoomService playerRoomService, IRoomsService roomsService, IPlayerService playerService, IGameStateService gameStateService, ITrackableIdGeneratorService trackableIdGeneratorService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _gameHub = gameHub;
        _playerRoomService = playerRoomService;
        _gameStateService = gameStateService;
        _trackableIdGeneratorService = trackableIdGeneratorService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.Information("FoodGeneratorWorkerService running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
            var rooms = _playerRoomService.GetRoomsPlayers();
            await Parallel.ForEachAsync(rooms, stoppingToken, ProcessRooms);
        }
    }

    private async ValueTask ProcessRooms(KeyValuePair<Guid, IList<IPlayer>> room, CancellationToken cancellationToken = default)
    {
        var gameState = _gameStateService.GetGameState(_trackableIdGeneratorService.Generate(room.Key));
        if (gameState is null) return;
        if (gameState.Players.Count == 0)
        {
            gameState.Foods.Clear();
            return;
        }
        if(!_gameStateService.CanGenerateFood(gameState)) return;
            
        var food = _gameStateService.GenerateFood(gameState.Room);
        await _gameHub.Clients.Group(room.Key.ToString()).FoodGenerated(new() { Food = food, Count = gameState.Foods.Count });

    }
}