using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Extensions;
using Domain.Game.Helpers;
using Domain.Game.Rules;
using Domain.Game.Scripting;
using Domain.Game.Stats;
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
    private readonly Random _random = Random.Shared;

    /// <summary>A shot closer to the head than this sets off a shield burst of a snake that uses its abilities by itself.</summary>
    private const double ShieldTriggerDistance = 120;

    /// <summary>Food in reach for a vacuum burst to be worth using by itself.</summary>
    private const int VacuumTriggerFoods = 3;

    /// <summary>Health under which a heal pulse is used by itself.</summary>
    private const double HealPulseTriggerHealth = 0.5;
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

    private readonly IStatsReporter? _statsReporter;
    /// <summary>The stats of the lives of signed-in snakes still playing. Guests are never in here.</summary>
    private readonly ConcurrentDictionary<Guid, LifeStats> _lifeStats = new();
    /// <summary>The stats of lives that ended, waiting for the player to be removed to be reported.</summary>
    private readonly ConcurrentDictionary<Guid, LifeStatsReport> _finishedStats = new();

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
        TimeProvider? clock = null,
        IStatsReporter? statsReporter = null)
    {
        _perkRules = perkRules;
        _clock = clock ?? TimeProvider.System;
        _statsReporter = statsReporter;
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

    public bool SetAutoAbilitiesByPlayerId(string playerConnectionId, bool active)
    {
        var player = _playerService.Get(playerConnectionId);
        if (player is null) return false;
        player.AutoAbilities = active;
        return true;
    }

    public bool UseAbilityByPlayerId(string playerConnectionId)
    {
        var player = _playerService.Get(playerConnectionId);
        if (player is not { IsAlive: true } || FindGameState(player) is not { } gameState) return false;
        var used = UseReadyAbilities(gameState, player, _ => true);
        if (used) UpdatePlayer(player);
        return used;
    }

    private IGameState? FindGameState(IPlayer player) => _gameState.Values.FirstOrDefault(state => state.Players.ContainsKey(player.Guid));

    /// <summary>
    /// Uses the cooldown abilities that are ready and that <paramref name="wanted"/> says yes to. The abilities of one perk are used
    /// together and recharge together. True when any was used.
    /// </summary>
    private bool UseReadyAbilities(IGameState gameState, IPlayer player, Func<AbilitySpec, bool> wanted)
    {
        var now = _clock.GetUtcNow();
        var used = false;
        var specs = _perkRules.AbilitiesFor(player.FoodsEaten, gameState.Room, GrantedOf(player), SurgesOf(player));
        foreach (var perk in specs.GroupBy(spec => spec.PerkId))
        {
            if (player.Abilities.TryGetValue(perk.Key, out var timer) && timer.ReadyAt > now) continue;
            if (!perk.Any(wanted)) continue;
            var first = perk.First();
            player.Abilities[perk.Key] = new AbilityTimer(now.AddMilliseconds(first.CooldownMilliseconds), now.AddMilliseconds(first.DurationMilliseconds));
            foreach (var spec in perk.Where(spec => spec.Effect == PerkEffect.HealPulse)) HealByPulse(player, spec.Amount);
            if (perk.Any(spec => spec.Effect == PerkEffect.ShieldBurst))
                player.ProtectedUntil = new[] { player.ProtectedUntil ?? now, now.AddMilliseconds(first.DurationMilliseconds) }.Max();
            used = true;
        }
        return used;
    }

    private void HealByPulse(IPlayer player, double hitPoints)
    {
        var maxHp = _damageCalculator.MaxSegmentHp(Math.Max(player.Path.Count, 1));
        player.SegmentHealth = _bodyHealthRules.Heal(player.SegmentHealth.ToArray(), maxHp, hitPoints).ToList();
    }

    /// <summary>For a snake that uses its abilities by itself: each ready one goes off when it would help.</summary>
    private void AutoUseAbilities(IGameState gameState, IPlayer player)
    {
        var now = _clock.GetUtcNow();
        if (!player.IsAlive || !player.Abilities.Values.Any(timer => timer.ReadyAt <= now)) return;
        UseReadyAbilities(gameState, player, spec => WouldHelp(gameState, player, spec));
    }

    private bool WouldHelp(IGameState gameState, IPlayer player, AbilitySpec spec)
    {
        var head = player.CurrentPosition;
        switch (spec.Effect)
        {
            case PerkEffect.ShieldBurst:
                return gameState.Obstacles.Values.Any(shot => !IsOwnShot(shot, player)
                    && Math.Sqrt(Math.Pow(shot.CurrentPosition.X - head.X, 2) + Math.Pow(shot.CurrentPosition.Y - head.Y, 2)) < ShieldTriggerDistance);
            case PerkEffect.Dash:
                return player.IsBoosting;
            case PerkEffect.HealPulse:
                return player.SegmentHealth.Any(health => health < HealPulseTriggerHealth);
            case PerkEffect.VacuumBurst:
                return gameState.Foods.Values.Count(food => food.Kind != FoodKind.Prize
                    && Math.Sqrt(Math.Pow(food.Position.X - head.X, 2) + Math.Pow(food.Position.Y - head.Y, 2)) <= spec.Amount) >= VacuumTriggerFoods;
            default:
                return false;
        }
    }

    public async Task MovePlayer(IGameState gameState, IPlayer player)
    {
        TurnTowardsTarget(player);
        if (player.AutoAbilities && player.Abilities.Count > 0) AutoUseAbilities(gameState, player);
        var perks = EffectsOf(gameState, player);
        var sugarRush = gameState.SugarRushEndsAt is { } rushEnds && _clock.GetUtcNow() < rushEnds;
        var boost = _boostRules.Step(player.IsBoosting, player.BoostTicks, player.Score, perks.BoostSpeedBonus, perks.BoostLengthFactor, sugarRush);
        player.BoostTicks = boost.BoostTicks;
        player.Score -= boost.MassSpent;
        if (boost.Active) StatsOf(player)?.Boosted(_tickRateService.TickRate);
        player.ShotCooldownTicks = _shotCooldownRules.Tick(player.ShotCooldownTicks);
        var tickRateMilliseconds = _tickRateService.TickRate.TotalMilliseconds;
        var speedFactor = _snakeConfiguration.Value.Speed * boost.SpeedMultiplier * (1 + perks.AbilitySpeedBonus);
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

        if (gameState.Maze is { } maze)
            newCurrentPosition = HoldAtWalls(gameState, maze, player, newCurrentPosition, perks.Invulnerable);

        player.CurrentPosition = newCurrentPosition;
        MoveBody(gameState, player, newCurrentPosition);
        RegenerateBody(player, perks);
        ContinueBurst(gameState, player, perks);
        StatsOf(player)?.Observe(player.Score, player.Path.Count);

        _playerService.Save(player);
    }
    /// <summary>A snake that touches a wall pays at most this often, so scraping along one is not charged every frame.</summary>
    private static readonly TimeSpan WallHitCooldown = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Keeps a head out of the walls of a maze: when the step would put it in one, it slides along the wall if it can, else stays where
    /// it was, and the snake pays the maze's wall cost (mass that falls as food), but never more than it has: a wall cannot kill. A head
    /// that is in a wall already (a snake that was there when the maze rose, or spawned there) may walk out, for free.
    /// </summary>
    private IPointAxis HoldAtWalls(IGameState gameState, WorldMaze maze, IPlayer player, IPointAxis wanted, bool invulnerable)
    {
        var size = _snakeConfiguration.Value.HeadSize;
        var from = player.CurrentPosition;
        if (!maze.BlocksSquare(wanted.X, wanted.Y, size) || maze.BlocksSquare(from.X, from.Y, size)) return wanted;

        var allowed = _pointAxisServices.CreateWith(wanted);
        allowed.X = from.X;
        allowed.Y = from.Y;
        var alongX = !maze.BlocksSquare(wanted.X, from.Y, size) ? Math.Abs(wanted.X - from.X) : -1;
        var alongY = !maze.BlocksSquare(from.X, wanted.Y, size) ? Math.Abs(wanted.Y - from.Y) : -1;
        if (alongX >= 0 && alongX >= alongY) allowed.X = wanted.X;
        else if (alongY >= 0) allowed.Y = wanted.Y;

        if (!invulnerable) PayForWall(gameState, maze, player, allowed);
        return allowed;
    }

    private void PayForWall(IGameState gameState, WorldMaze maze, IPlayer player, IPointAxis at)
    {
        var now = _clock.GetUtcNow();
        if (maze.WallHits.TryGetValue(player.Guid, out var last) && now - last < WallHitCooldown) return;
        maze.WallHits[player.Guid] = now;
        var lost = Math.Min(Math.Max(player.Score, 0), maze.WallMassLoss);
        if (lost <= 0) return;
        player.Score -= lost;
        AddPellet(gameState, new PelletDrop(new Point(at.X, at.Y), lost), player.BackgroundColor);
        TellLoss(gameState, player, lost);
    }

    /// <summary>Whether a shot that goes from one spot to another touches a wall on the way. It looks every half block, so no wall is jumped over.</summary>
    private bool ShotHitsWall(WorldMaze maze, IPointAxis from, IPointAxis to)
    {
        var half = _snakeConfiguration.Value.HeadSize / 2.0;
        var (fromX, fromY) = (from.X + half, from.Y + half);
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / (maze.Unit / 2)));
        for (var step = 1; step <= steps; step++)
            if (maze.BlocksPoint(fromX + dx * step / steps, fromY + dy * step / steps)) return true;
        return false;
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
            FinishStats(player);
            ReportStats(player);
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
        StatsOf(player)?.Ate(food.Kind);
        GrantPerk(player, food);
        ApplyReward(gameState, player, food.Reward);
        RefreshPerks(gameState, player);
        HealBody(player, food.Value);
        ApplyEatingRewards(gameState, player, food);

    }

    /// <summary>Works out again which perks a snake has, at which tiers, for the foods it has eaten and what it was given.</summary>
    private void RefreshPerks(IGameState gameState, IPlayer player)
    {
        var surges = SurgesOf(player);
        player.Perks = _perkRules.UnlockedIds(player.FoodsEaten, gameState.Room, GrantedOf(player)).ToList();
        player.PerkTiers = _perkRules.TiersFor(player.FoodsEaten, gameState.Room, GrantedOf(player), surges).ToList();
        _scriptedEffects.TryRemove(player.Guid, out _);
        var effects = BaseEffectsOf(gameState, player);
        player.AutoFire = effects.AutoFire;
        player.LivesLeft = Math.Max(0, effects.ExtraLives + player.BonusLives - player.LivesUsed);
        SyncAbilities(gameState, player);
    }

    /// <summary>Gives the snake a timer for each cooldown perk it has (ready, the first time) and drops the timers of perks it no longer has.</summary>
    private void SyncAbilities(IGameState gameState, IPlayer player)
    {
        var owned = _perkRules.AbilitiesFor(player.FoodsEaten, gameState.Room, GrantedOf(player), SurgesOf(player)).Select(spec => spec.PerkId).ToHashSet();
        foreach (var gone in player.Abilities.Keys.Where(id => !owned.Contains(id)).ToArray()) player.Abilities.Remove(gone);
        foreach (var id in owned.Where(id => !player.Abilities.ContainsKey(id))) player.Abilities[id] = default;
    }

    private static IReadOnlyDictionary<string, int>? SurgesOf(IPlayer player)
        => player.TierSurges.Count == 0 ? null : new Dictionary<string, int>(player.TierSurges);

    /// <summary>
    /// Gives what a reward gives, apart from the perk it names: mass, lives, a mend, a random perk the snake lacks and a tier surge. Works
    /// out the perks again only when the caller does; the caller always does right after.
    /// </summary>
    private void ApplyReward(IGameState gameState, IPlayer player, PerkReward? reward)
    {
        if (reward is null || reward.IsEmpty || reward.Problem() is not null) return;
        if (reward.BonusMass > 0) player.Score += reward.BonusMass;
        if (reward.ExtraLives > 0) player.BonusLives = Math.Min(PerkReward.MaxLives, player.BonusLives + reward.ExtraLives);
        if (reward.Mend) player.SegmentHealth = player.SegmentHealth.Select(_ => 1.0).ToList();
        if (reward.GrantsRandomPerk) GrantRandomPerk(gameState, player);
        if (reward.HasTierSurge) SurgeTier(gameState, player, reward);
        foreach (var other in reward.Alongside) ApplyReward(gameState, player, other);
    }

    private void GrantRandomPerk(IGameState gameState, IPlayer player)
    {
        var candidates = _perkRules.UnownedPerkIds(player.FoodsEaten, gameState.Room, GrantedOf(player));
        if (candidates.Count == 0) return;
        player.GrantedPerks.Add(candidates[_random.Next(candidates.Count)]);
    }

    /// <summary>Lifts a perk the snake has by the reward's tiers: the named perk, or a random one that can still go up. Nothing when none can.</summary>
    private void SurgeTier(IGameState gameState, IPlayer player, PerkReward reward)
    {
        var candidates = _perkRules.SurgeCandidates(player.FoodsEaten, gameState.Room, GrantedOf(player), SurgesOf(player));
        if (candidates.Count == 0) return;
        var perkId = string.IsNullOrWhiteSpace(reward.TierSurgePerk) ? candidates[_random.Next(candidates.Count)] : reward.TierSurgePerk;
        if (!candidates.Contains(perkId)) return;
        var least = Math.Max(1, reward.TierSurgeMin);
        var most = Math.Max(least, reward.TierSurgeMax);
        var lifted = _random.Next(least, most + 1);
        var already = player.TierSurges.TryGetValue(perkId, out var kept) ? kept : 0;
        player.TierSurges[perkId] = Math.Min(already + lifted, _perkRules.TierCountOf(perkId) - 1);
    }

    /// <summary>Foods that something the snake did counts for, toward its perks. They can lift a perk to a higher tier.</summary>
    private void CreditFoods(IGameState gameState, IPlayer player, int foods)
    {
        if (foods <= 0) return;
        player.FoodsEaten += foods;
        RefreshPerks(gameState, player);
        UpdatePlayer(player);
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
        foreach (var direction in VolleyDirections(player, 1 + perks.ExtraShots, perks.SpreadFactor))
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
        StatsOf(player)?.ShotFired();
    }

    /// <summary>The way each shot of a volley goes: one straight along the snake, more fanned out around it.</summary>
    private IEnumerable<IMovementDirection> VolleyDirections(IPlayer player, int shots, double spreadFactor = 1)
    {
        if (shots == 1) return [player.Direction];
        var aim = _headingRules.AngleOf(player.Direction.Axis.X, player.Direction.Axis.Y);
        return _perkRules.ShotOffsets(shots, spreadFactor).Select(offset => DirectionAt(player, aim + offset)).ToArray();
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
            if (KillPlayer(gameState, player)) RegisterKill(gameState, collisions[player.Guid], player);
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
        if (GetPlayer(gameState, impact.Shot.CreatedyBy) is { } shooter) StatsOf(shooter)?.ShotHit(0);
        if (!KillPlayer(gameState, player)) return;
        gameState.Hits.Enqueue(new DamageDealt(impact.Shot.CreatedyBy.Guid, player.Guid, head.X, head.Y, 0, DamageKind.Kill));
        RegisterKill(gameState, impact.Shot.CreatedyBy.Guid, player);
    }

    private void DamageBody(IGameState gameState, IPlayer player, ShotImpact impact)
    {
        var targetLength = player.Path.Count;
        var shooter = GetPlayer(gameState, impact.Shot.CreatedyBy);
        var shooterLength = shooter?.Path.Count ?? InitialLength;
        var damage = _damageCalculator.ShotDamage(shooterLength, targetLength) * EffectsOf(gameState, player).DamageFactor;
        var maxHp = _damageCalculator.MaxSegmentHp(targetLength);
        var hitAt = player.Path[impact.Hit.SegmentIndex];
        var hpBefore = player.SegmentHealth[impact.Hit.SegmentIndex] * maxHp;
        var outcome = _bodyShotRules.ApplyDamage(ToBodyState(player), impact.Hit.SegmentIndex, damage,
            maxHp, canBreak: targetLength > InitialLength);
        RecordDamage(gameState, impact, player, hitAt, Math.Min(damage, hpBefore), outcome.SegmentBroken);
        CountBodyShot(shooter, Math.Min(damage, hpBefore), outcome.SegmentBroken);
        ApplyBody(player, outcome.Body);
        if (outcome.BrokenAt.HasValue)
            DropBrokenSegment(gameState, player, outcome.BrokenAt.Value);
        UpdatePlayer(player);
    }

    /// <summary>A shot of a signed-in snake that hurt another snake's body: it counts as a hit, with the damage it did, and a segment broken off.</summary>
    private void CountBodyShot(IPlayer? shooter, double damage, bool segmentBroken)
    {
        if (shooter is null) return;
        var stats = StatsOf(shooter);
        stats?.ShotHit(damage);
        if (segmentBroken) stats?.SegmentBroken();
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
        CountBossKill(gameState, shot.CreatedyBy.Guid);
        DropBossReward(gameState, boss);
        return true;
    }

    private void CountBossKill(IGameState gameState, Guid shooterId)
    {
        if (!gameState.Players.TryGetValue(shooterId, out var shooter)) return;
        StatsOf(shooter)?.BroughtDownBoss();
    }

    private void DropBossReward(IGameState gameState, WorldBoss boss)
    {
        var ring = Enumerable.Range(0, 16)
            .Select(index => new Point(boss.X + Math.Cos(index * Math.PI / 8) * boss.Size, boss.Y + Math.Sin(index * Math.PI / 8) * boss.Size))
            .ToArray();
        foreach (var drop in _deathDropRules.Plan(boss.RewardMass, ring)) AddPellet(gameState, drop, new Color(BossRewardColor, BossRewardColor));
        if (boss.LastHitBy is not { } killerId || !gameState.Players.TryGetValue(killerId, out var killer) || !killer.IsAlive) return;
        GrantBossPerk(gameState, killer, boss);
    }

    private void GrantBossPerk(IGameState gameState, IPlayer killer, WorldBoss boss)
    {
        if (!string.IsNullOrWhiteSpace(boss.GrantsPerk) && !killer.GrantedPerks.Contains(boss.GrantsPerk)) killer.GrantedPerks.Add(boss.GrantsPerk);
        killer.FoodsEaten += Math.Max(0, boss.FoodsCredit);
        ApplyReward(gameState, killer, boss.Reward);
        RefreshPerks(gameState, killer);
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
                if (KillPlayer(gameState, player)) died.Add(player);
                continue;
            }
            var lost = Math.Min(player.Score, Resisted(gameState, player, PerkRules.Zone, mass));
            if (lost <= 0) continue;
            player.Score -= lost;
            AddPellet(gameState, new PelletDrop(new Point(player.CurrentPosition.X, player.CurrentPosition.Y), lost), player.BackgroundColor);
            TellLoss(gameState, player, lost);
            UpdatePlayer(player);
        }
        return died;
    }

    public IReadOnlyList<IPlayer> StrikeArea(IGameState gameState, double x, double y, double radius, WorldStrikeKind kind = WorldStrikeKind.Lightning)
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
                var mass = player.Score;
                CountStrike(player, kind, mass);
                if (!KillPlayer(gameState, player)) continue;
                died.Add(player);
                TellLoss(gameState, player, mass);
            }
            else if (hit.BrokenSegments.Count > 0)
            {
                var before = player.Score;
                var broken = Spared(hit.BrokenSegments, EffectsOf(gameState, player).ResistanceTo(kind == WorldStrikeKind.Meteor ? PerkRules.Meteors : PerkRules.Lightning));
                if (broken.Count == 0) continue;
                BreakSegments(gameState, player, broken);
                TellLoss(gameState, player, before - player.Score);
                CountStrike(player, kind, before - player.Score);
            }
        }
        return died;
    }

    public void ResolveMines(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        if (gameState.Mines.IsEmpty) return;
        var now = _clock.GetUtcNow();
        var half = _snakeConfiguration.Value.HeadSize / 2.0;
        var heads = gameState.Players.Values
            .Where(player => player.IsAlive && !EffectsOf(gameState, player).Invulnerable)
            .Select(player => new Point(player.CurrentPosition.X + half, player.CurrentPosition.Y + half))
            .ToArray();
        foreach (var mine in gameState.Mines.Values.Where(mine => mine.ArmedAt <= now).ToArray())
        {
            var touched = heads.Any(head => Math.Sqrt(Math.Pow(head.X - mine.X, 2) + Math.Pow(head.Y - mine.Y, 2)) <= mine.TriggerRadius);
            if (touched && gameState.Mines.TryRemove(mine.Id, out _)) Blast(gameState, mine);
        }
    }

    /// <summary>A mine going off breaks the body segments within its reach, like a strike, but never kills a head.</summary>
    private void Blast(IGameState gameState, WorldMine mine)
    {
        var headSize = _snakeConfiguration.Value.HeadSize;
        foreach (var player in gameState.Players.Values.Where(player => player.IsAlive).ToArray())
        {
            if (EffectsOf(gameState, player).Invulnerable) continue;
            var hit = WorldEventRules.Resolve(ToPoints(player.Path), new Point(mine.X, mine.Y), mine.Radius, headSize, InitialLength);
            var broken = Spared(hit.BrokenSegments, EffectsOf(gameState, player).ResistanceTo(PerkRules.Mines));
            if (broken.Count == 0) continue;
            var before = player.Score;
            BreakSegments(gameState, player, broken);
            TellLoss(gameState, player, before - player.Score);
        }
    }

    /// <summary>The mass an event would take from a snake, less the share its perks spare it. Never nothing while there is something to take.</summary>
    private long Resisted(IGameState gameState, IPlayer player, string kind, long mass)
    {
        var resistance = EffectsOf(gameState, player).ResistanceTo(kind);
        return resistance <= 0 ? mass : Math.Max(1, (long)Math.Round(mass * (1 - resistance), MidpointRounding.AwayFromZero));
    }

    /// <summary>The segments that still break once the snake's resistance spares its share of them; the nearest to the impact break first.</summary>
    private static IReadOnlyList<int> Spared(IReadOnlyList<int> broken, double resistance)
    {
        if (resistance <= 0 || broken.Count == 0) return broken;
        return broken.Take((int)Math.Round(broken.Count * (1 - resistance), MidpointRounding.AwayFromZero)).ToArray();
    }

    /// <summary>Killing the snake that is wanted pays the killer in mass and in foods, once, and takes the mark off.</summary>
    private void PayBounty(IGameState gameState, IPlayer killer, IPlayer victim)
    {
        if (gameState.Bounty is not { } bounty || bounty.Target != victim.Guid) return;
        gameState.Bounty = null;
        killer.Score += bounty.RewardMass;
        ApplyReward(gameState, killer, bounty.Reward);
        killer.FoodsEaten += Math.Max(0, bounty.FoodsCredit);
        RefreshPerks(gameState, killer);
        UpdatePlayer(killer);
    }

    /// <summary>A strike counts in the stats of a snake only when it cost it mass.</summary>
    private void CountStrike(IPlayer player, WorldStrikeKind kind, long massLost)
    {
        if (massLost <= 0) return;
        StatsOf(player)?.Struck(kind);
    }

    /// <summary>Tells a snake how much mass it lost to something that is not a shot, at its head. Nothing when it lost none.</summary>
    private static void TellLoss(IGameState gameState, IPlayer player, long mass)
    {
        if (mass <= 0) return;
        gameState.Hits.Enqueue(new DamageDealt(Guid.Empty, player.Guid, player.CurrentPosition.X, player.CurrentPosition.Y, mass, DamageKind.Lost));
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
        => WithTemporaryEffects(gameState, player, BaseEffectsOf(gameState, player));

    private PerkEffects BaseEffectsOf(IGameState gameState, IPlayer player)
    {
        var surges = SurgesOf(player);
        if (!_perkRules.HasScripts(player.FoodsEaten, gameState.Room, GrantedOf(player))) return _perkRules.EffectsFor(player.FoodsEaten, gameState.Room, GrantedOf(player), surges);
        var now = _clock.GetUtcNow();
        if (_scriptedEffects.TryGetValue(player.Guid, out var kept) && now - kept.At < ScriptedEffectsLifetime) return kept.Effects;
        var effects = _perkRules.EffectsFor(player.FoodsEaten, gameState.Room, ContextOf(gameState, player), GrantedOf(player), surges);
        _scriptedEffects[player.Guid] = (now, effects);
        return effects;
    }

    /// <summary>What lasts for a moment on top of the perks: the protection after an extra life, and the cooldown abilities that are on.</summary>
    private PerkEffects WithTemporaryEffects(IGameState gameState, IPlayer player, PerkEffects effects)
    {
        var now = _clock.GetUtcNow();
        if (player.ProtectedUntil > now) effects = effects with { Invulnerable = true };
        if (!player.Abilities.Values.Any(timer => timer.ActiveUntil > now)) return effects;
        foreach (var spec in _perkRules.AbilitiesFor(player.FoodsEaten, gameState.Room, GrantedOf(player), SurgesOf(player)))
        {
            if (!player.Abilities.TryGetValue(spec.PerkId, out var timer) || timer.ActiveUntil <= now) continue;
            effects = spec.Effect switch
            {
                PerkEffect.ShieldBurst => effects with { Invulnerable = true },
                PerkEffect.Dash => effects with { AbilitySpeedBonus = effects.AbilitySpeedBonus + spec.Amount },
                PerkEffect.VacuumBurst => effects with { MagnetRadius = effects.MagnetRadius + spec.Amount },
                _ => effects
            };
        }
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
        StatsOf(killer)?.Killed();
        CreditFoods(gameState, killer, _perkRules.KillFoodsCredit);
        PayBounty(gameState, killer, victim);
        killer.Score += (long)Math.Round(victim.Score * EffectsOf(gameState, killer).PlunderShare, MidpointRounding.AwayFromZero);
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

    /// <summary>Kills the snake, unless a life saves it. True when it died.</summary>
    private bool KillPlayer(IGameState gameState, IPlayer player)
    {
        if (!player.IsAlive) return false;
        if (TrySaveWithLife(gameState, player)) return false;
        player.IsAlive = false;
        ForgetLife(player);
        StatsOf(player)?.Died();
        FinishStats(player);
        DropPellets(gameState, player);
        UpdatePlayer(player);
        return true;
    }

    /// <summary>
    /// Spends an extra life, if the snake has one: it keeps a share of its mass (the rest falls as food where it was), is healed, and
    /// cannot be hurt for a few seconds. The client is told with a <see cref="DamageKind.Saved"/> notice.
    /// </summary>
    private bool TrySaveWithLife(IGameState gameState, IPlayer player)
    {
        var lives = BaseEffectsOf(gameState, player).ExtraLives + player.BonusLives - player.LivesUsed;
        if (lives <= 0) return false;
        player.LivesUsed++;
        player.LivesLeft = lives - 1;
        var kept = (long)Math.Floor(player.Score * _perkRules.ExtraLifeKeepShare);
        foreach (var drop in _deathDropRules.Plan(player.Score - kept, ToPoints(player.Path))) AddPellet(gameState, drop, player.BackgroundColor);
        player.Score = kept;
        player.SegmentHealth = player.SegmentHealth.Select(_ => 1.0).ToList();
        player.ProtectedUntil = _clock.GetUtcNow().AddSeconds(_perkRules.ExtraLifeProtectSeconds);
        var head = player.CurrentPosition;
        gameState.Hits.Enqueue(new DamageDealt(Guid.Empty, player.Guid, head.X, head.Y, player.LivesLeft, DamageKind.Saved));
        UpdatePlayer(player);
        return true;
    }

    public void AttachAccount(IPlayer player, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.AccountId = accountId;
        _lifeStats.TryAdd(player.Guid, LifeStats.Begin(_clock.GetUtcNow(), player.Score, player.Path.Count));
    }

    public LifeStatsReport? FinishedStatsOf(IPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return _finishedStats.TryGetValue(player.Guid, out var report) ? report : null;
    }

    /// <summary>The stats of the life the snake is playing; null for a guest, and once the life is over.</summary>
    private LifeStats? StatsOf(IPlayer player)
        => player.AccountId is not null && _lifeStats.TryGetValue(player.Guid, out var stats) ? stats : null;

    /// <summary>Ends the life of a signed-in snake, if it is still counted: its last mass and length count, and its report is made.</summary>
    private void FinishStats(IPlayer player)
    {
        if (player.AccountId is null || !_lifeStats.TryRemove(player.Guid, out var stats)) return;
        stats.Observe(player.Score, player.Path.Count);
        _finishedStats[player.Guid] = stats.Finish(_clock.GetUtcNow(), Guid.NewGuid());
    }

    /// <summary>Sends the report of a life that ended, once: it is forgotten as it goes.</summary>
    private void ReportStats(IPlayer player)
    {
        if (player.AccountId is not Guid accountId || !_finishedStats.TryRemove(player.Guid, out var report)) return;
        _statsReporter?.Report(accountId, report);
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


            if (gameState.Maze is { } maze && ShotHitsWall(maze, obstacle.CurrentPosition, newCurrentPosition))
            {
                toRemove.Add(id);
                continue;
            }

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