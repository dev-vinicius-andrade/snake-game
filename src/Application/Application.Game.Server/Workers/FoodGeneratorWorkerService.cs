using Application.Game.Server.Entities.Configurations;
using Application.Game.Server.Hubs;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Exceptions;
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
    private readonly PrizeSpawner _prizeSpawner;
    private readonly PerkWorld _perkWorld;
    public FoodGeneratorWorkerService(Serilog.ILogger logger, IOptions<AppSettings> appSettings, IHubContext<GameHub, IGameHubSendEvents> gameHub, IPlayerRoomService playerRoomService, IRoomsService roomsService, IPlayerService playerService, IGameStateService gameStateService, ITrackableIdGeneratorService trackableIdGeneratorService, PrizeSpawner prizeSpawner, PerkWorld perkWorld)
    {
        _prizeSpawner = prizeSpawner;
        _perkWorld = perkWorld;
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
        try
        {
            await FeedRoom(room);
        }
        catch (RoomNotFoundException)
        {
            // The room was closed while this pass ran. One room going away must never stop the server.
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error(exception, "Could not generate food for room {RoomId}", room.Key);
        }
    }

    private async Task FeedRoom(KeyValuePair<Guid, IList<IPlayer>> room)
    {
        var gameState = _gameStateService.GetGameState(_trackableIdGeneratorService.Generate(room.Key));
        if (gameState is null) return;
        _prizeSpawner.Update(gameState);
        _perkWorld.Update(gameState);
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