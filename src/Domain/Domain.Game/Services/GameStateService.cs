using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Extensions;
using Domain.Game.Helpers;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;
using Color = Domain.Game.Entities.Color;

namespace Domain.Game.Services;

public class GameStateService : IGameStateService
{
    private readonly IPlayerService _playerService;
    private readonly IPlayerRoomService _playerRoomService;
    private readonly IRoomsService _roomsService;
    private readonly ConcurrentDictionary<Guid, IGameState> _gameState;
    private readonly IOptions<SnakeConfiguration> _snakeConfiguration;
    private readonly IOptions<ServerConfiguration> _serverConfiguration;
    private readonly ITrackableIdGeneratorService _trackableIdGeneratorService;
    private readonly IKnowDirectionsService _knowDirectionsService;
    private readonly IPointAxisServices _pointAxisServices;
    private readonly ITickRateService _tickRateService;
    private readonly IObstacleService _obstacleService;

    public GameStateService(
        IOptions<ServerConfiguration> serverConfiguration,
        IOptions<SnakeConfiguration> snakeConfiguration,
        IPlayerService playerService,
        IPlayerRoomService playerRoomService,
        IRoomsService roomsService,
        ConcurrentDictionary<Guid, IGameState> gameState,
        ITrackableIdGeneratorService trackableIdGeneratorService,
        IKnowDirectionsService knowDirectionsService,
        IPointAxisServices pointAxisServices,
        ITickRateService tickRateService, IObstacleService obstacleService)
    {
        _playerService = playerService;
        _playerRoomService = playerRoomService;
        _roomsService = roomsService;
        _gameState = gameState;

        _snakeConfiguration = snakeConfiguration;
        _trackableIdGeneratorService = trackableIdGeneratorService;
        _knowDirectionsService = knowDirectionsService;
        _serverConfiguration = serverConfiguration;
        _pointAxisServices = pointAxisServices;
        _tickRateService = tickRateService;
        _obstacleService = obstacleService;
    }

    public async Task<IPlayer?> CreatePlayerInRoom(string playerId, string nickname, Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var randomDirection = RandomDirection();
        if (randomDirection is null)
            return null;
        var room = _roomsService.GetRoom(_trackableIdGeneratorService.Generate(roomId));
        if (room is null)
            return null;
        var gameState = CreateGameState(room);
        var initialDirection = _playerService.CreateDirection(randomDirection.Value.Key, randomDirection.Value.Value);
        var playerColor = GetAvailableColor(room);
        var initialPosition = GenerateInitialPosition(2, room.Width, room.Height);
        var initialPaths = GenerateInitialPath(initialPosition, initialDirection.Axis);
        var player = _playerService.Create(playerId, nickname, playerColor, initialDirection, initialPosition, initialPaths);
        _playerService.Save(player);
        if (!gameState.Players.ContainsKey(player.Guid))
            gameState.Players.Add(player.Guid, player);
        return !_playerRoomService.ConnectPlayer(player, room) ? null : player;
    }

    public IGameState CreateGameState(IRoom room)
    {
        if (_gameState.TryGetValue(room.Guid, out var gameState)) return gameState;
        gameState = new GameState(room);


        _gameState.TryAdd(room.Guid, gameState);
        return gameState;
    }

    public bool ChangeDirectionByPlayerId(string playerConnectionId, Direction? direction = null)
    {
        if (direction is null) return false;
        var player = _playerService.Get(playerConnectionId);
        if (player is null)
            return false;
        if (player.CurrentDirection == direction) return true;
        player.CurrentDirection = direction.Value;
        var newDirection = _knowDirectionsService.Get(player.CurrentDirection);
        if (newDirection is null) return false;
        player.Direction = _playerService.CreateDirection(newDirection.Value.Key, newDirection.Value.Value);

        return UpdatePlayer(player);

    }

    public async Task MovePlayer(IGameState gameState, IPlayer player)
    {
        var tickRateMilliseconds = _tickRateService.TickRate.TotalMilliseconds;
        var speedFactor = _snakeConfiguration.Value.Speed;
        var movementPerTickX = player.Direction.Axis.X * speedFactor / (1000 / tickRateMilliseconds);
        var movementPerTickY = player.Direction.Axis.Y * speedFactor / (1000 / tickRateMilliseconds);
        var newCurrentPosition = _pointAxisServices.CreateWith(player.CurrentPosition);
        newCurrentPosition.X += movementPerTickX * _snakeConfiguration.Value.HeadSize;
        newCurrentPosition.Y += movementPerTickY * _snakeConfiguration.Value.HeadSize;
        if (newCurrentPosition.X < 0)
            newCurrentPosition.X = gameState.Room.Width - _snakeConfiguration.Value.HeadSize;
        if (newCurrentPosition.X >= gameState.Room.Width)
            newCurrentPosition.X = 0;
        if (newCurrentPosition.Y < 0)
            newCurrentPosition.Y = gameState.Room.Height - _snakeConfiguration.Value.HeadSize;
        if (newCurrentPosition.Y >= gameState.Room.Height)
            newCurrentPosition.Y = 0;

        player.CurrentPosition = newCurrentPosition;
        player.Path.Add(newCurrentPosition);
        player.Path.Remove(player.Path.First());

        _playerService.Save(player);
    }
    public bool CanGenerateFood(IGameState gameState)
    {
        return gameState.Foods.Count < _serverConfiguration.Value.RoomsConfiguration.MaxFoods;
    }

    public async Task<(IPlayer player, string oldPlayerConnectionId)?> ReconnectPlayer(ITrackableId roomTrackableId, ITrackableId playerTrackableId, string playerConnectionId)
    {
        try
        {
            var gameState = GetGameState(roomTrackableId);
            if (!gameState.Players.TryGetValue(playerTrackableId.Guid, out var player)) return null;
            var oldPlayerConnectionId = player.Id;
            player.Id = playerConnectionId;
            UpdatePlayer(player);
            return (player, oldPlayerConnectionId);

        }
        catch (Exception e)
        {
            return null;
        }
    }

    public async Task<bool> DisconnectPlayer(ITrackableId roomTrackableId, ITrackableId playerTrackableId)
    {
        try
        {
            var gameState = GetGameState(roomTrackableId);
            if (!gameState.Players.TryGetValue(playerTrackableId.Guid, out var player)) return false;
            gameState.Players.Remove(playerTrackableId.Guid);
            _playerRoomService.DisconnectPlayer(player, gameState.Room);
            _playerService.Delete(player);
            return true;
        }
        catch (Exception)
        {

            return false;
        }
    }

    public async Task CheckFoodCollision(IGameState gameState, IPlayer player)
    {

        var food = gameState.Foods.Values.FirstOrDefault(f => PlayerCurrentPositionCollideWithFood(f, player));
        if (food is null) return;
        gameState.Foods.Remove(food.Guid);
        player.Score += 1;

    }

    private bool PlayerCurrentPositionCollideWithFood(IFood food, IPlayer player)
    {
        var playerCurrentPosition = player.CurrentPosition;
        var foodPosition = food.Position;
        return playerCurrentPosition.X <= foodPosition.X + _snakeConfiguration.Value.HeadSize &&
               playerCurrentPosition.X + _snakeConfiguration.Value.HeadSize >= foodPosition.X &&
               playerCurrentPosition.Y <= foodPosition.Y + _snakeConfiguration.Value.HeadSize &&
               playerCurrentPosition.Y + _snakeConfiguration.Value.HeadSize >= foodPosition.Y;

    }

    public async Task<IList<IScore>> GetScore(IGameState gameState, int skip = 0, int take = 10)
    {
        var scores = gameState.Players.Values.OrderByDescending(p => p.Score).Skip(skip).Take(take).Select(p =>
            new Score
            {
                CurrentScore = p.Score,
                Nickname = p.Name

            } as IScore).ToList();
        return scores;
    }

    public async Task Shoot(IGameState gameState, IRoom room, IPlayer player)
    {
        if (player.Score <= 0) return;
        var id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
        while (gameState.Obstacles.ContainsKey(id.Guid))
            id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
        var obstacle = _obstacleService.Create(id, room, player.CurrentPosition, player.Direction, _snakeConfiguration.Value.HeadSize, player);
        gameState.Obstacles.Add(obstacle.Guid, obstacle);

        var newScore = player.Score - 1;
        if (newScore < 0) newScore = 0;
        player.Score = newScore;
        UpdatePlayer(player);

    }

    public async Task CheckObstacleCollision(IGameState gameState, IPlayer player)
    {
        var obstacle = gameState.Obstacles.Values.FirstOrDefault(f => PlayerCurrentPositionCollideWithObstacle(f, player));
        if (obstacle is null) return ;
        if (_trackableIdGeneratorService.IsType<IPlayer>(obstacle.CreatedyBy))
        {
            if (obstacle.CreatedyBy.Guid == player.Guid) return ;
            var playerObstacleOrigin = GetPlayer(gameState, obstacle.CreatedyBy);
            if (playerObstacleOrigin is null) return;
            playerObstacleOrigin.Score += player.Score;
        }




        player.IsAlive = false;
        
        UpdatePlayer(player);
        await DisconnectPlayer(gameState.Room, player);


    }

    public IPlayer? GetPlayer(IGameState gameState, ITrackableId trackableId)
    {
        return gameState.Players.TryGetValue(trackableId.Guid, out var player) ? player : null;
    }

    public Task MoveObstacles(IGameState gameState)
    {
        var toRemove = new List<Guid>();

        foreach (var obstacleRecord in gameState.Obstacles)
        {
            var id = obstacleRecord.Key;
            var obstacle = obstacleRecord.Value;

            var tickRateMilliseconds = _tickRateService.TickRate.TotalMilliseconds;
            var speedFactor = _snakeConfiguration.Value.Speed * 2;
            var movementPerTickX = obstacle.Direction.Axis.X * speedFactor / (1000 / tickRateMilliseconds);
            var movementPerTickY = obstacle.Direction.Axis.Y * speedFactor / (1000 / tickRateMilliseconds);

            var newCurrentPosition = _pointAxisServices.CreateWith(obstacle.CurrentPosition);
            newCurrentPosition.X += movementPerTickX * _snakeConfiguration.Value.HeadSize;
            newCurrentPosition.Y += movementPerTickY * _snakeConfiguration.Value.HeadSize;


            obstacle.CurrentPosition = newCurrentPosition;
            if (_obstacleService.IsOnEndPosition(obstacle))
                toRemove.Add(id); 
        }

        foreach (var id in toRemove) gameState.Obstacles.Remove(id);

        return Task.CompletedTask;

    }

    private bool PlayerCurrentPositionCollideWithObstacle(IObstacle obstacle, IPlayer player)
    {
        var playerCurrentPosition = player.CurrentPosition;
        var obstaclePosition = obstacle.CurrentPosition;
        return playerCurrentPosition.X <= obstaclePosition.X + _snakeConfiguration.Value.HeadSize &&
               playerCurrentPosition.X + _snakeConfiguration.Value.HeadSize >= obstaclePosition.X &&
               playerCurrentPosition.Y <= obstaclePosition.Y + _snakeConfiguration.Value.HeadSize &&
               playerCurrentPosition.Y + _snakeConfiguration.Value.HeadSize >= obstaclePosition.Y;

    }

    private bool UpdatePlayer(IPlayer player)
    {

        foreach (var (roomId, gameState) in _gameState)
        {
            if (!gameState.Players.ContainsKey(player.Guid)) continue;
            _playerService.Save(player);
            _playerRoomService.UpdatePlayer(player, gameState.Room);
            _gameState[roomId].Players[player.Guid] = player;
            return true;
        }
        return false;
    }

    public IList<IPointAxis> GenerateInitialPath(IPointAxis initialPosition, IPointAxis initialDirection)
    {
        var initialPath = new List<IPointAxis>().AddValue(initialPosition);
        for (var size = 1; size <= _snakeConfiguration.Value.InitialSnakeSize; size++)
            initialPath
                .Insert(0, new PointAxis()
                {

                    X = initialPosition.X + (initialDirection.X * size * _snakeConfiguration.Value.HeadSize),
                    Y = initialPosition.Y + (initialDirection.Y * size * _snakeConfiguration.Value.HeadSize)
                });
        return initialPath;
    }

    public KeyValuePair<Direction, IPointAxis>? RandomDirection()
    {
        var randomNumber = RandomHelper.RandomNumber(0, _knowDirectionsService.Count);
        return _knowDirectionsService.Knows(randomNumber)
            ? _knowDirectionsService.Get(randomNumber)
            : RandomDirection();
    }

    public IPointAxis GenerateInitialPosition(in int snakeConfigurationHeadSize,
        in int xMaxValue, in int yMaxValue)
        =>
            RandomHelper.RandomPosition(
                xMinValue: 0,
                xMaxValue: xMaxValue,
                yMinValue: 0,
                yMaxValue: yMaxValue);
    public IColor GetAvailableColor(IRoom room)
    {
        var color = RandomHelper.RandomColor();
        return IsColorBeeingUsed(room, color)
            ? GetAvailableColor(room)
            : new Color(color, color);

    }

    public IPointAxis GetAvailablePosition(IRoom room)
    {
        var position = RandomHelper.RandomPosition(
                       xMinValue: 0,
                                  xMaxValue: room.Width,
                                  yMinValue: 0,
                                  yMaxValue: room.Height);
        return HasPositionBeeingUsed(room, position)
            ? GetAvailablePosition(room)
            : position;

    }
    public IFood GenerateFood(IRoom room)
    {
        var game = GetGameState(room);
        var color = GetRandomAvailableColor(room);
        var position = RandomHelper.RandomPosition(
            xMinValue: 0,
            xMaxValue: room.Width,
            yMinValue: 0,
            yMaxValue: room.Height);
        if (game.Foods.ContainsKey(Guid.NewGuid())) return GenerateFood(room);
        var food = new Food(Guid.NewGuid())
        {
            Position = position,
            Color = new Color(color, color)

        };

        game.Foods.Add(food.Guid, food);
        return food;
    }
    public string GetRandomAvailableColor(IRoom room)
    {
        var color = RandomHelper.RandomColor();
        return IsColorBeeingUsed(room, color)
            ? GetRandomAvailableColor(room)
            : color;
    }
    public bool IsColorBeeingUsed(IRoom room, string color)
    => AnyCharWithColor(room, color) || AnyFoodWithColor(room, color);
    public bool AnyCharWithColor(IRoom room, string color) => GetGameState(room).Players.Values.Any(p => p.BackgroundColor.FillColor == color);
    public bool AnyFoodWithColor(IRoom room, string color) => GetGameState(room).Foods.Any(p => p.Value.Color.FillColor == color);
    public virtual bool HasPositionBeeingUsed(IRoom room, IPointAxis position, int delta = 0)
        => AnyCharInPosition(room, position, delta) || AnyFoodInPosition(room, position, delta);

    public bool AnyCharInPosition(IRoom room, IPointAxis position, int delta = 0)
        => GetGameState(room).Players.Values.Select(p => p.CurrentPosition).ToList().GetNearBy(position, delta).Any();


    public bool AnyFoodInPosition(IRoom room, IPointAxis position, int delta = 0)
        => GetGameState(room).Foods.Select(c => c.Value.Position).ToList().GetNearBy(position, delta).Any();

    public IGameState GetGameState(ITrackableId roomId)
    {
        if (_gameState.TryGetValue(roomId.Guid, out var gameState)) return gameState;
        var room = _roomsService.GetRoom(roomId);
        if (room is null && _roomsService.CanCreate())
            room = _roomsService.CreateRoom();
        else if (room is null)
            throw new UnableToCreateRoomException();

        gameState = new GameState(room);
        _gameState.TryAdd(roomId.Guid, gameState);

        return gameState;
    }

}