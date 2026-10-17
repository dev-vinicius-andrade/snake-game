using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Extensions;
using Domain.Game.Helpers;
using Domain.Game.Rules;
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
    private readonly SnakeSizeRules _snakeSizeRules;
    private readonly SnakeCollisionRules _snakeCollisionRules;
    private readonly DeathDropRules _deathDropRules;
    private readonly SnakeBodyRules _snakeBodyRules;
    private readonly BodyShotRules _bodyShotRules;
    private readonly BodyHealthRules _bodyHealthRules;
    private readonly BoostRules _boostRules;
    private readonly DamageCalculator _damageCalculator;
    private readonly SnakeRulesOptions _rulesOptions;
    private readonly ShotCooldownRules _shotCooldownRules;

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
        ITickRateService tickRateService, IObstacleService obstacleService,
        SnakeSizeRules snakeSizeRules, SnakeCollisionRules snakeCollisionRules, DeathDropRules deathDropRules,
        SnakeBodyRules snakeBodyRules, BodyShotRules bodyShotRules, BodyHealthRules bodyHealthRules,
        BoostRules boostRules, DamageCalculator damageCalculator, SnakeRulesOptions rulesOptions,
        ShotCooldownRules shotCooldownRules)
    {
        _shotCooldownRules = shotCooldownRules;
        _snakeBodyRules = snakeBodyRules;
        _bodyShotRules = bodyShotRules;
        _bodyHealthRules = bodyHealthRules;
        _boostRules = boostRules;
        _damageCalculator = damageCalculator;
        _rulesOptions = rulesOptions;
        _snakeSizeRules = snakeSizeRules;
        _snakeCollisionRules = snakeCollisionRules;
        _deathDropRules = deathDropRules;
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

    public bool SetBoostByPlayerId(string playerConnectionId, bool active)
    {
        var player = _playerService.Get(playerConnectionId);
        if (player is null) return false;
        player.IsBoosting = active;
        return UpdatePlayer(player);
    }

    public async Task MovePlayer(IGameState gameState, IPlayer player)
    {
        var boost = _boostRules.Step(player.IsBoosting, player.BoostTicks, player.Score);
        player.BoostTicks = boost.BoostTicks;
        player.Score -= boost.MassSpent;
        player.ShotCooldownTicks = _shotCooldownRules.Tick(player.ShotCooldownTicks);
        var tickRateMilliseconds = _tickRateService.TickRate.TotalMilliseconds;
        var speedFactor = _snakeConfiguration.Value.Speed * boost.SpeedMultiplier;
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
        MoveBody(gameState, player, newCurrentPosition);

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
        if (!player.IsAlive) return;

        var food = gameState.Foods.Values.FirstOrDefault(f => PlayerCurrentPositionCollideWithFood(f, player));
        if (food is null) return;
        gameState.Foods.Remove(food.Guid);
        player.Score += food.Value;
        HealBody(player, food.Value);

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
        if (!player.IsAlive || player.Score <= 0 || !_shotCooldownRules.CanShoot(player.ShotCooldownTicks)) return;
        var id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
        while (gameState.Obstacles.ContainsKey(id.Guid))
            id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
        var obstacle = _obstacleService.Create(id, room, player.CurrentPosition, player.Direction, _snakeConfiguration.Value.HeadSize, player);
        gameState.Obstacles.Add(obstacle.Guid, obstacle);

        var newScore = player.Score - 1;
        if (newScore < 0) newScore = 0;
        player.Score = newScore;
        player.ShotCooldownTicks = _shotCooldownRules.CooldownTicks(_tickRateService.TickRate);
        UpdatePlayer(player);

    }

    public async Task CheckObstacleCollision(IGameState gameState, IPlayer player)
    {
        if (!player.IsAlive) return;
        var impact = FindShotImpact(gameState, player);
        if (impact is null) return;
        gameState.Obstacles.Remove(impact.Shot.Guid);
        if (impact.Hit.IsHead)
            KillPlayer(gameState, player);
        else
            DamageBody(gameState, player, impact);
    }

    public Task ResolveSnakeCollisions(IGameState gameState)
    {
        var livingPlayers = gameState.Players.Values.Where(player => player.IsAlive).ToArray();
        var bodies = livingPlayers.Select(ToSnakeBody).ToArray();
        var collided = _snakeCollisionRules.FindCollided(bodies, _snakeConfiguration.Value.HeadSize);
        foreach (var player in livingPlayers.Where(player => collided.Contains(player.Guid)))
            KillPlayer(gameState, player);
        return Task.CompletedTask;
    }

    private sealed record ShotImpact(IObstacle Shot, ShotHit Hit);

    private int InitialLength => _snakeConfiguration.Value.InitialSnakeSize + 1;

    private ShotImpact? FindShotImpact(IGameState gameState, IPlayer player)
    {
        var segments = ToPoints(player.Path);
        var headSize = _snakeConfiguration.Value.HeadSize;
        return gameState.Obstacles.Values
            .Where(shot => !IsOwnShot(shot, player))
            .Select(shot => new { Shot = shot, Hit = _bodyShotRules.FindHit(segments, ToPoint(shot.CurrentPosition), headSize) })
            .Where(candidate => candidate.Hit.HasValue)
            .Select(candidate => new ShotImpact(candidate.Shot, candidate.Hit!.Value))
            .FirstOrDefault();
    }

    private void DamageBody(IGameState gameState, IPlayer player, ShotImpact impact)
    {
        var targetLength = player.Path.Count;
        var shooterLength = GetPlayer(gameState, impact.Shot.CreatedyBy)?.Path.Count ?? InitialLength;
        var damage = _damageCalculator.ShotDamage(shooterLength, targetLength);
        var outcome = _bodyShotRules.ApplyDamage(ToBodyState(player), impact.Hit.SegmentIndex, damage,
            _damageCalculator.MaxSegmentHp(targetLength), canBreak: targetLength > InitialLength);
        ApplyBody(player, outcome.Body);
        if (outcome.BrokenAt.HasValue)
            DropBrokenSegment(gameState, player, outcome.BrokenAt.Value);
        UpdatePlayer(player);
    }

    private void DropBrokenSegment(IGameState gameState, IPlayer player, Point at)
    {
        var lostMass = (long)Math.Ceiling(_rulesOptions.MassPerSegment);
        player.Score = Math.Max(0, player.Score - lostMass);
        AddPellet(gameState, new PelletDrop(at, lostMass), player.BackgroundColor);
    }

    private void HealBody(IPlayer player, long fruitValue)
    {
        var maxHp = _damageCalculator.MaxSegmentHp(Math.Max(player.Path.Count, 1));
        var healed = _bodyHealthRules.Heal(player.SegmentHealth.ToArray(), maxHp, _damageCalculator.HealAmount(fruitValue));
        player.SegmentHealth = healed.ToList();
    }

    private void MoveBody(IGameState gameState, IPlayer player, IPointAxis head)
    {
        var targetLength = _snakeSizeRules.TargetLength(InitialLength, player.Score);
        var world = new WorldBounds(gameState.Room.Width, gameState.Room.Height);
        var body = _snakeBodyRules.Move(ToBodyState(player), ToPoint(head), targetLength, _snakeConfiguration.Value.HeadSize, world);
        ApplyBody(player, body);
    }

    private void ApplyBody(IPlayer player, BodyState body)
    {
        player.Path = body.Segments.Select(point => ToAxis(player.CurrentPosition, point)).ToList();
        player.SegmentHealth = body.Health.ToList();
    }

    private IPointAxis ToAxis(IPointAxis template, Point point)
    {
        var axis = _pointAxisServices.CreateWith(template);
        axis.X = point.X;
        axis.Y = point.Y;
        return axis;
    }

    private static BodyState ToBodyState(IPlayer player)
        => new(ToPoints(player.Path), player.SegmentHealth.ToArray());

    private static Point ToPoint(IPointAxis axis) => new(axis.X, axis.Y);

    private bool IsOwnShot(IObstacle obstacle, IPlayer player)
        => _trackableIdGeneratorService.IsType<IPlayer>(obstacle.CreatedyBy) && obstacle.CreatedyBy.Guid == player.Guid;

    private static SnakeBody ToSnakeBody(IPlayer player)
        => new(player.Guid, ToPoints(player.Path));

    private static Point[] ToPoints(IEnumerable<IPointAxis> path)
        => path.Select(ToPoint).ToArray();

    private void KillPlayer(IGameState gameState, IPlayer player)
    {
        if (!player.IsAlive) return;
        player.IsAlive = false;
        DropPellets(gameState, player);
        UpdatePlayer(player);
    }

    private void DropPellets(IGameState gameState, IPlayer player)
    {
        foreach (var drop in _deathDropRules.Plan(player.Score, ToPoints(player.Path)))
            AddPellet(gameState, drop, player.BackgroundColor);
    }

    private void AddPellet(IGameState gameState, PelletDrop drop, IColor color)
    {
        var pellet = new Food(Guid.NewGuid())
        {
            Position = _pointAxisServices.Create((int)drop.Position.X, (int)drop.Position.Y),
            Color = color,
            Value = drop.Value
        };
        gameState.Foods.Add(pellet.Guid, pellet);
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