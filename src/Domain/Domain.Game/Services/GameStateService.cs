using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Extensions;
using Domain.Game.Helpers;
using Domain.Game.Rules;
using Domain.Game.Scripting;
using Library.Commons.Game.Domain.Entities;
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
    /// <summary>The colour of the food a boss drops.</summary>
    private const string BossRewardColor = "#ffd23f";

    /// <summary>World units a magnet pulls food closer to the head each tick.</summary>
    private const double MagnetPullPerTick = 6;

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
    private readonly HeadingRules _headingRules;
    private readonly PerkRules _perkRules;
    private readonly TimeProvider _clock;
    /// <summary>What a player's perks did when last worked out, kept for a moment: scripts are not asked 60 times a second.</summary>
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset At, PerkEffects Effects)> _scriptedEffects = new();
    private static readonly TimeSpan ScriptedEffectsLifetime = TimeSpan.FromMilliseconds(250);

    /// <summary>What a script needs to know about a snake's life: when it began, whom it killed, and what the scripts remember.</summary>
    private sealed class SnakeLife(DateTimeOffset spawnedAt)
    {
        public DateTimeOffset SpawnedAt { get; } = spawnedAt;
        public int Kills { get; set; }
        public ScriptStates States { get; } = new();
    }

    private readonly ConcurrentDictionary<Guid, SnakeLife> _lives = new();

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
        ShotCooldownRules shotCooldownRules, HeadingRules headingRules, PerkRules perkRules,
        TimeProvider? clock = null)
    {
        _perkRules = perkRules;
        _clock = clock ?? TimeProvider.System;
        _shotCooldownRules = shotCooldownRules;
        _headingRules = headingRules;
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
        player.CurrentDirection = randomDirection.Value.Key;
        player.Heading = player.TargetHeading = _headingRules.AngleOf(initialDirection.Axis.X, initialDirection.Axis.Y);
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
        if (direction is null or Direction.Angular) return false;
        var player = _playerService.Get(playerConnectionId);
        if (player is null)
            return false;
        if (player.CurrentDirection == direction) return true;
        var newDirection = _knowDirectionsService.Get(direction.Value);
        if (newDirection is null) return false;
        player.CurrentDirection = direction.Value;
        player.Direction = _playerService.CreateDirection(newDirection.Value.Key, newDirection.Value.Value);
        player.Heading = player.TargetHeading = _headingRules.AngleOf(newDirection.Value.Value.X, newDirection.Value.Value.Y);

        return UpdatePlayer(player);

    }

    public bool SteerByPlayerId(string playerConnectionId, double angle)
    {
        if (!double.IsFinite(angle)) return false;
        var player = _playerService.Get(playerConnectionId);
        if (player is null) return false;
        player.TargetHeading = _headingRules.Normalize(angle);
        player.CurrentDirection = Direction.Angular;
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
        TurnTowardsTarget(player);
        var perks = EffectsOf(gameState, player);
        var boost = _boostRules.Step(player.IsBoosting, player.BoostTicks, player.Score, perks.BoostSpeedBonus, perks.BoostLengthFactor);
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
        RegenerateBody(player, perks);
        ContinueBurst(gameState, player, perks);

        _playerService.Save(player);
    }
    private void TurnTowardsTarget(IPlayer player)
    {
        var turned = _headingRules.Turn(player.Heading, player.TargetHeading, _headingRules.MaxTurn(_tickRateService.TickRate));
        if (turned == player.Heading) return;
        player.Heading = turned;
        var axis = ToAxis(player.CurrentPosition, _headingRules.AxisOf(turned));
        axis.Z = 0;
        axis.Angle = turned;
        player.Direction = _playerService.CreateDirection(Direction.Angular, axis);
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
            ForgetLife(player);
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

        PullFoodToward(gameState, player);
        var food = gameState.Foods.Values.FirstOrDefault(f => PlayerCurrentPositionCollideWithFood(f, player));
        if (food is null) return;
        gameState.Foods.Remove(food.Guid);
        player.Score += food.Value;
        player.FoodsEaten += food.FoodsCredit;
        GrantPerk(player, food);
        player.Perks = _perkRules.UnlockedIds(player.FoodsEaten, gameState.Room, GrantedOf(player)).ToList();
        player.AutoFire = EffectsOf(gameState, player).AutoFire;
        HealBody(player, food.Value);
        ApplyEatingRewards(gameState, player, food);

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
        if (!player.IsAlive || !_shotCooldownRules.CanShoot(player.ShotCooldownTicks)) return;
        var perks = EffectsOf(gameState, player);
        if (player.Score <= 0 && !perks.FreeShots) return;
        FireVolley(gameState, room, player, perks);

        if (!perks.FreeShots) player.Score = Math.Max(0, player.Score - 1);
        player.BurstRemaining = perks.BurstExtraShots;
        player.BurstTicks = BurstIntervalTicks;
        player.ShotCooldownTicks = ReloadTicks(perks) + perks.BurstExtraShots * BurstIntervalTicks;
        UpdatePlayer(player);

    }

    private int BurstIntervalTicks
        => Math.Max(1, (int)Math.Ceiling(_perkRules.BurstIntervalMilliseconds / _tickRateService.TickRate.TotalMilliseconds - 0.01));

    private void FireVolley(IGameState gameState, IRoom room, IPlayer player, PerkEffects perks)
    {
        foreach (var direction in VolleyDirections(player, 1 + perks.ExtraShots))
            FireShot(gameState, room, player, direction, perks);
    }

    /// <summary>The rest of a burst: one volley each time the burst timer runs out, for free.</summary>
    private void ContinueBurst(IGameState gameState, IPlayer player, PerkEffects perks)
    {
        if (player.BurstRemaining <= 0) return;
        if (!player.IsAlive)
        {
            player.BurstRemaining = 0;
            return;
        }
        player.BurstTicks--;
        if (player.BurstTicks > 0) return;
        FireVolley(gameState, gameState.Room, player, perks);
        player.BurstRemaining--;
        player.BurstTicks = BurstIntervalTicks;
    }

    private void RegenerateBody(IPlayer player, PerkEffects perks)
    {
        if (perks.RegenPerSecond <= 0 || player.SegmentHealth.All(ratio => ratio >= 1)) return;
        var maxHp = _damageCalculator.MaxSegmentHp(Math.Max(player.Path.Count, 1));
        var amount = perks.RegenPerSecond * _tickRateService.TickRate.TotalSeconds;
        player.SegmentHealth = _bodyHealthRules.Heal(player.SegmentHealth.ToArray(), maxHp, amount).ToList();
    }

    /// <summary>Food close to a snake with a magnet drifts toward its head. The prize is left alone: it is to be fought for.</summary>
    private void PullFoodToward(IGameState gameState, IPlayer player)
    {
        var radius = EffectsOf(gameState, player).MagnetRadius;
        if (radius <= 0) return;
        foreach (var food in gameState.Foods.Values.Where(food => food.Kind != FoodKind.Prize))
            PullToward(food, player.CurrentPosition, radius);
    }

    private void PullToward(IFood food, IPointAxis head, double radius)
    {
        var dx = head.X - food.Position.X;
        var dy = head.Y - food.Position.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > radius || distance < 1e-9) return;
        var step = Math.Min(MagnetPullPerTick, distance);
        food.Position = _pointAxisServices.Create(
            (int)Math.Round(food.Position.X + dx / distance * step),
            (int)Math.Round(food.Position.Y + dy / distance * step));
    }

    private int ReloadTicks(PerkEffects perks)
        => Math.Max(1, (int)Math.Ceiling(_shotCooldownRules.CooldownTicks(_tickRateService.TickRate) * perks.ReloadFactor));

    private void FireShot(IGameState gameState, IRoom room, IPlayer player, IMovementDirection direction, PerkEffects perks)
    {
        var id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
        while (gameState.Obstacles.ContainsKey(id.Guid))
            id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
        var obstacle = _obstacleService.Create(id, room, player.CurrentPosition, direction, _snakeConfiguration.Value.HeadSize, player);
        obstacle.SpeedFactor = perks.ShotSpeedFactor;
        obstacle.Piercing = perks.Piercing;
        gameState.Obstacles.Add(obstacle.Guid, obstacle);
    }

    /// <summary>The way each shot of a volley goes: one straight along the snake, more fanned out around it.</summary>
    private IEnumerable<IMovementDirection> VolleyDirections(IPlayer player, int shots)
    {
        if (shots == 1) return [player.Direction];
        var aim = _headingRules.AngleOf(player.Direction.Axis.X, player.Direction.Axis.Y);
        return _perkRules.ShotOffsets(shots).Select(offset => DirectionAt(player, aim + offset)).ToArray();
    }

    private IMovementDirection DirectionAt(IPlayer player, double angle) => DirectionAt(player.CurrentPosition, angle);

    private IMovementDirection DirectionAt(IPointAxis template, double angle)
    {
        var axis = ToAxis(template, _headingRules.AxisOf(_headingRules.Normalize(angle)));
        axis.Z = 0;
        axis.Angle = angle;
        return _playerService.CreateDirection(Direction.Angular, axis);
    }

    public async Task CheckObstacleCollision(IGameState gameState, IPlayer player)
    {
        if (!player.IsAlive) return;
        var impact = FindShotImpact(gameState, player);
        if (impact is null) return;
        if (impact.Shot.Piercing) impact.Shot.HitPlayers.Add(player.Guid);
        else gameState.Obstacles.Remove(impact.Shot.Guid);
        if (EffectsOf(gameState, player).Invulnerable) return;
        if (impact.Hit.IsHead)
            KillByShot(gameState, player, impact);
        else
            DamageBody(gameState, player, impact);
    }

    public Task ResolveSnakeCollisions(IGameState gameState)
    {
        var livingPlayers = gameState.Players.Values.Where(player => player.IsAlive).ToArray();
        var bodies = livingPlayers.Select(ToSnakeBody).ToArray();
        var collisions = _snakeCollisionRules.FindCollisions(bodies, _snakeConfiguration.Value.HeadSize);
        foreach (var player in livingPlayers.Where(player => collisions.ContainsKey(player.Guid) && !EffectsOf(gameState, player).Invulnerable))
        {
            KillPlayer(gameState, player);
            RegisterKill(gameState, collisions[player.Guid], player);
        }
        return Task.CompletedTask;
    }

    private sealed record ShotImpact(IObstacle Shot, ShotHit Hit);

    private int InitialLength => _snakeConfiguration.Value.InitialSnakeSize + 1;

    private ShotImpact? FindShotImpact(IGameState gameState, IPlayer player)
    {
        var segments = ToPoints(player.Path);
        var headSize = _snakeConfiguration.Value.HeadSize;
        return gameState.Obstacles.Values
            .Where(shot => !IsOwnShot(shot, player) && !shot.HitPlayers.Contains(player.Guid))
            .Select(shot => new { Shot = shot, Hit = _bodyShotRules.FindHit(segments, ToPoint(shot.CurrentPosition), headSize) })
            .Where(candidate => candidate.Hit.HasValue)
            .Select(candidate => new ShotImpact(candidate.Shot, candidate.Hit!.Value))
            .FirstOrDefault();
    }

    private void KillByShot(IGameState gameState, IPlayer player, ShotImpact impact)
    {
        var head = player.CurrentPosition;
        gameState.Hits.Enqueue(new DamageDealt(impact.Shot.CreatedyBy.Guid, player.Guid, head.X, head.Y, 0, DamageKind.Kill));
        KillPlayer(gameState, player);
        RegisterKill(gameState, impact.Shot.CreatedyBy.Guid, player);
    }

    private void DamageBody(IGameState gameState, IPlayer player, ShotImpact impact)
    {
        var targetLength = player.Path.Count;
        var shooterLength = GetPlayer(gameState, impact.Shot.CreatedyBy)?.Path.Count ?? InitialLength;
        var damage = _damageCalculator.ShotDamage(shooterLength, targetLength) * EffectsOf(gameState, player).DamageFactor;
        var maxHp = _damageCalculator.MaxSegmentHp(targetLength);
        var hitAt = player.Path[impact.Hit.SegmentIndex];
        var hpBefore = player.SegmentHealth[impact.Hit.SegmentIndex] * maxHp;
        var outcome = _bodyShotRules.ApplyDamage(ToBodyState(player), impact.Hit.SegmentIndex, damage,
            maxHp, canBreak: targetLength > InitialLength);
        RecordDamage(gameState, impact, player, hitAt, Math.Min(damage, hpBefore), outcome.SegmentBroken);
        ApplyBody(player, outcome.Body);
        if (outcome.BrokenAt.HasValue)
            DropBrokenSegment(gameState, player, outcome.BrokenAt.Value);
        UpdatePlayer(player);
    }

    public void FireTurret(IGameState gameState, IReadOnlyList<double> angles)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(angles);
        if (gameState.Boss is not { } boss) return;
        var middle = _snakeConfiguration.Value.HeadSize / 2.0;
        var origin = ToAxis(_pointAxisServices.Create(0, 0), new Point(boss.X - middle, boss.Y - middle));
        foreach (var angle in angles)
        {
            var id = _trackableIdGeneratorService.Generate(Guid.NewGuid());
            var obstacle = _obstacleService.Create(id, gameState.Room, origin, DirectionAt(origin, angle), _snakeConfiguration.Value.HeadSize,
                _trackableIdGeneratorService.Generate(boss.Id));
            obstacle.SpeedFactor = boss.ShotSpeed;
            gameState.Obstacles.Add(obstacle.Guid, obstacle);
        }
    }

    public Task ResolveBossHits(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        if (gameState.Boss is not { } boss) return Task.CompletedTask;
        var headSize = _snakeConfiguration.Value.HeadSize;
        foreach (var shot in gameState.Obstacles.Values.ToArray())
        {
            if (shot.CreatedyBy.Guid == boss.Id || shot.HitPlayers.Contains(boss.Id)) continue;
            var middle = new Point(shot.CurrentPosition.X + headSize / 2.0, shot.CurrentPosition.Y + headSize / 2.0);
            if (!WorldEventRules.TurretIsHit(boss.X, boss.Y, boss.Size, middle, headSize)) continue;
            if (shot.Piercing) shot.HitPlayers.Add(boss.Id);
            else gameState.Obstacles.Remove(shot.Guid);
            if (HitBoss(gameState, boss, shot)) break;
        }
        return Task.CompletedTask;
    }

    /// <summary>One hit on the boss. True when it was the last: the boss is gone and its reward given.</summary>
    private bool HitBoss(IGameState gameState, WorldBoss boss, IObstacle shot)
    {
        boss.Hp -= 1;
        boss.LastHitBy = shot.CreatedyBy.Guid;
        var down = boss.Hp <= 0;
        gameState.Hits.Enqueue(new DamageDealt(shot.CreatedyBy.Guid, boss.Id, boss.X, boss.Y, 1, down ? DamageKind.Kill : DamageKind.Hit));
        if (!down) return false;
        gameState.Boss = null;
        DropBossReward(gameState, boss);
        return true;
    }

    private void DropBossReward(IGameState gameState, WorldBoss boss)
    {
        var ring = Enumerable.Range(0, 16)
            .Select(index => new Point(boss.X + Math.Cos(index * Math.PI / 8) * boss.Size, boss.Y + Math.Sin(index * Math.PI / 8) * boss.Size))
            .ToArray();
        foreach (var drop in _deathDropRules.Plan(boss.RewardMass, ring)) AddPellet(gameState, drop, new Color(BossRewardColor, BossRewardColor));
        if (boss.LastHitBy is not { } killerId || !gameState.Players.TryGetValue(killerId, out var killer) || !killer.IsAlive) return;
        GrantBossPerk(gameState, killer, boss.GrantsPerk);
    }

    private void GrantBossPerk(IGameState gameState, IPlayer killer, string? perk)
    {
        if (string.IsNullOrWhiteSpace(perk)) return;
        if (!killer.GrantedPerks.Contains(perk)) killer.GrantedPerks.Add(perk);
        killer.Perks = _perkRules.UnlockedIds(killer.FoodsEaten, gameState.Room, GrantedOf(killer)).ToList();
        killer.AutoFire = EffectsOf(gameState, killer).AutoFire;
        UpdatePlayer(killer);
    }

    public IReadOnlyList<IPlayer> ApplyZoneDamage(IGameState gameState, double x, double y, double radius, long mass)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var died = new List<IPlayer>();
        var half = _snakeConfiguration.Value.HeadSize / 2.0;
        var room = gameState.Room;
        foreach (var player in gameState.Players.Values.Where(player => player.IsAlive).ToArray())
        {
            var head = new Point(player.CurrentPosition.X + half, player.CurrentPosition.Y + half);
            if (!WorldEventRules.IsOutsideZone(head, new Point(x, y), radius, room.Width, room.Height)) continue;
            if (EffectsOf(gameState, player).Invulnerable) continue;
            if (player.Score <= 0)
            {
                KillPlayer(gameState, player);
                died.Add(player);
                continue;
            }
            var lost = Math.Min(player.Score, mass);
            player.Score -= lost;
            AddPellet(gameState, new PelletDrop(new Point(player.CurrentPosition.X, player.CurrentPosition.Y), lost), player.BackgroundColor);
            UpdatePlayer(player);
        }
        return died;
    }

    public IReadOnlyList<IPlayer> StrikeArea(IGameState gameState, double x, double y, double radius)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var died = new List<IPlayer>();
        var headSize = _snakeConfiguration.Value.HeadSize;
        foreach (var player in gameState.Players.Values.Where(player => player.IsAlive).ToArray())
        {
            if (EffectsOf(gameState, player).Invulnerable) continue;
            var hit = WorldEventRules.Resolve(ToPoints(player.Path), new Point(x, y), radius, headSize, InitialLength);
            if (hit.HeadHit)
            {
                KillPlayer(gameState, player);
                died.Add(player);
            }
            else if (hit.BrokenSegments.Count > 0)
                BreakSegments(gameState, player, hit.BrokenSegments);
        }
        return died;
    }

    private void BreakSegments(IGameState gameState, IPlayer player, IReadOnlyList<int> indices)
    {
        var body = ToBodyState(player);
        var lost = indices.ToHashSet();
        var kept = Enumerable.Range(0, body.Segments.Count).Where(index => !lost.Contains(index)).ToArray();
        ApplyBody(player, body with
        {
            Segments = kept.Select(index => body.Segments[index]).ToArray(),
            Health = kept.Select(index => body.Health[index]).ToArray()
        });
        foreach (var index in indices) DropBrokenSegment(gameState, player, body.Segments[index]);
        UpdatePlayer(player);
    }

    private static void RecordDamage(IGameState gameState, ShotImpact impact, IPlayer target, IPointAxis at, double damage, bool broke)
        => gameState.Hits.Enqueue(new DamageDealt(
            impact.Shot.CreatedyBy.Guid, target.Guid, at.X, at.Y, damage, broke ? DamageKind.Broke : DamageKind.Hit));

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
        player.Trail = body.Trail.Select(point => ToAxis(player.CurrentPosition, point)).ToList();
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
        => new(ToPoints(player.Path), player.SegmentHealth.ToArray(), ToPoints(player.Trail));

    private static Point ToPoint(IPointAxis axis) => new(axis.X, axis.Y);

    /// <summary>
    /// What a player's perks do, here and now. A snake whose perks have no script in this room gets it straight from the rules; with
    /// scripts the answer is kept for a quarter of a second.
    /// </summary>
    private PerkEffects EffectsOf(IGameState gameState, IPlayer player)
    {
        if (!_perkRules.HasScripts(player.FoodsEaten, gameState.Room, GrantedOf(player))) return _perkRules.EffectsFor(player.FoodsEaten, gameState.Room, GrantedOf(player));
        var now = _clock.GetUtcNow();
        if (_scriptedEffects.TryGetValue(player.Guid, out var kept) && now - kept.At < ScriptedEffectsLifetime) return kept.Effects;
        var effects = _perkRules.EffectsFor(player.FoodsEaten, gameState.Room, ContextOf(gameState, player), GrantedOf(player));
        _scriptedEffects[player.Guid] = (now, effects);
        return effects;
    }

    private static IReadOnlyCollection<string> GrantedOf(IPlayer player) => player.GrantedPerks.ToArray();

    /// <summary>A prize can give a perk, whatever the foods the snake has eaten. It is kept until the snake dies.</summary>
    private static void GrantPerk(IPlayer player, IFood food)
    {
        if (string.IsNullOrWhiteSpace(food.GrantsPerk) || player.GrantedPerks.Contains(food.GrantsPerk)) return;
        player.GrantedPerks.Add(food.GrantsPerk);
    }

    private ScriptContext ContextOf(IGameState gameState, IPlayer player)
    {
        var health = player.SegmentHealth;
        var now = _clock.GetUtcNow();
        var life = LifeOf(player);
        var snake = new ScriptSnake(
            player.Name, player.Path.Count, player.Score, player.FoodsEaten,
            health.Count == 0 ? 1.0 : health.Average(), health.Count == 0 ? 1.0 : health.Min(),
            player.IsBoosting, player.Perks.ToArray(),
            now.ToUnixTimeMilliseconds(), (long)Math.Max(0, (now - life.SpawnedAt).TotalMilliseconds), life.Kills);
        var room = gameState.Room;
        var view = new ScriptRoom(
            gameState.Players.Count, room.Name ?? string.Empty, room.Code ?? string.Empty, room.Width, room.Height, now.ToUnixTimeMilliseconds());
        return new ScriptContext(snake, view, life.States);
    }

    private SnakeLife LifeOf(IPlayer player) => _lives.GetOrAdd(player.Guid, _ => new SnakeLife(_clock.GetUtcNow()));

    private void ForgetLife(IPlayer player)
    {
        _lives.TryRemove(player.Guid, out _);
        _scriptedEffects.TryRemove(player.Guid, out _);
    }

    /// <summary>A kill counts for the killer's life, and the scripts of its perks may give something for it. Kills of the snake itself do not count.</summary>
    private void RegisterKill(IGameState gameState, Guid killerId, IPlayer victim)
    {
        if (killerId == victim.Guid || !gameState.Players.TryGetValue(killerId, out var killer) || !killer.IsAlive) return;
        LifeOf(killer).Kills++;
        _scriptedEffects.TryRemove(killer.Guid, out _);
        if (!_perkRules.HasScripts(killer.FoodsEaten, gameState.Room, GrantedOf(killer))) return;
        var rewards = _perkRules.RewardsForKilling(
            killer.FoodsEaten, gameState.Room, ContextOf(gameState, killer), new ScriptVictim(victim.Name, victim.Path.Count, victim.Score), GrantedOf(killer));
        ApplyRewards(killer, rewards);
        _scriptedEffects.TryRemove(killer.Guid, out _);
    }

    /// <summary>What the scripts of the snake's perks give for eating this: extra mass, and healing in HP.</summary>
    private void ApplyEatingRewards(IGameState gameState, IPlayer player, IFood food)
    {
        if (!_perkRules.HasScripts(player.FoodsEaten, gameState.Room, GrantedOf(player))) return;
        var rewards = _perkRules.RewardsForEating(
            player.FoodsEaten, gameState.Room, ContextOf(gameState, player), new ScriptFood(food.Value, food.Kind == FoodKind.Prize), GrantedOf(player));
        ApplyRewards(player, rewards);
        _scriptedEffects.TryRemove(player.Guid, out _);
    }

    private void ApplyRewards(IPlayer player, ScriptRewards rewards)
    {
        player.Score += rewards.Mass;
        if (rewards.Heal <= 0) return;
        var maxHp = _damageCalculator.MaxSegmentHp(Math.Max(player.Path.Count, 1));
        player.SegmentHealth = _bodyHealthRules.Heal(player.SegmentHealth.ToArray(), maxHp, rewards.Heal).ToList();
    }

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
        ForgetLife(player);
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
            var speedFactor = _snakeConfiguration.Value.Speed * 2 * obstacle.SpeedFactor;
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

    public void RemoveGameState(ITrackableId roomId) => _gameState.TryRemove(roomId.Guid, out _);

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