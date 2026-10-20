using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Color = Domain.Game.Entities.Color;

namespace Domain.Game.Tests;

/// <summary>Wiring of the pure rules inside <see cref="GameStateService"/>: real rules, real entities, faked collaborators.</summary>
public sealed partial class GameStateServiceTests
{
    private const int HeadSize = 15;
    private const int InitialLength = 6;

    private readonly PointAxisServices _axes = new();
    private readonly IPlayerService _playerService = Substitute.For<IPlayerService>();
    private readonly IObstacleService _obstacleService = Substitute.For<IObstacleService>();
    private readonly IKnowDirectionsService _knownDirections = Substitute.For<IKnowDirectionsService>();
    private readonly IPlayerRoomService _playerRooms = Substitute.For<IPlayerRoomService>();
    private readonly IRoomsService _roomsService = Substitute.For<IRoomsService>();
    private readonly DamageCalculator _damage = new(new DamageModelOptions());
    private readonly PerkRules _perks = new(new PerkOptions
    {
        ShotSpreadDegrees = 10,
        Perks =
        [
            new() { Id = "twin", Name = "Twin", FoodsRequired = 2, Effect = PerkEffect.ExtraShots, Amount = 1 },
            new() { Id = "reload", Name = "Reload", FoodsRequired = 3, Effect = PerkEffect.FasterReload, Amount = 0.5 },
            new() { Id = "swift", Name = "Swift", FoodsRequired = 4, Effect = PerkEffect.BoostSpeed, Amount = 0.8 }
        ]
    });
    private readonly GameStateService _service;
    private readonly ConcurrentDictionary<Guid, IGameState> _states;
    private readonly ITickRateService _tickRate = Substitute.For<ITickRateService>();
    private readonly SnakeRulesOptions _rules = new();
    private readonly FakeClock _clock = new();

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan span) => _now += span;
    }
    private readonly IGameState _state;
    private readonly IRoom _room = Substitute.For<IRoom>();

    public GameStateServiceTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1920;
        _room.Height = 1080;
        _state = new GameState(_room);
        _states = new ConcurrentDictionary<Guid, IGameState> { [_room.Guid] = _state };
        _tickRate.TickRate.Returns(TimeSpan.FromMilliseconds(1000 / 60.0));
        _playerService.CreateDirection(default, default!).ReturnsForAnyArgs(call =>
            new MovementDirection(call.ArgAt<Direction>(0), call.ArgAt<IPointAxis>(1)));
        _obstacleService.Create(default!, default!, default!, default!, default, default!).ReturnsForAnyArgs(call =>
            new Obstacle(Guid.NewGuid(), HeadSize, _room, call.ArgAt<ITrackableId>(5))
            {
                InitialPosition = call.ArgAt<IPointAxis>(2),
                CurrentPosition = call.ArgAt<IPointAxis>(2),
                EndPosition = _axes.Create(5000, 5000),
                Direction = call.ArgAt<IMovementDirection>(3)
            });
        _service = Build(_perks);
    }

    private GameStateService Build(PerkRules perks, IStatsReporter? statsReporter = null)
    {
        var rules = _rules;
        var states = _states;
        var tickRate = _tickRate;
        return new GameStateService(
            Options.Create(new ServerConfiguration()),
            Options.Create(new SnakeConfiguration { Speed = 15, HeadSize = HeadSize, InitialSnakeSize = InitialLength - 1 }),
            _playerService,
            _playerRooms,
            _roomsService,
            states,
            new TrackableIdGeneratorService(),
            _knownDirections,
            _axes,
            tickRate,
            _obstacleService,
            new SnakeSizeRules(rules),
            new SnakeCollisionRules(),
            new DeathDropRules(rules),
            new SnakeBodyRules(),
            new BodyShotRules(),
            new BodyHealthRules(new BodyHealer()),
            new BoostRules(rules),
            _damage,
            rules,
            new ShotCooldownRules(rules),
            new HeadingRules(rules),
            perks,
            _clock,
            statsReporter);
    }

    private IPointAxis At(double x, double y)
    {
        var axis = _axes.Create(0, 0);
        axis.X = x;
        axis.Y = y;
        return axis;
    }

    private Player AddPlayer(string name, double headX, double headY, int length = InitialLength, long score = 0)
    {
        var path = Enumerable.Range(0, length).Select(i => At(headX - (length - 1 - i) * HeadSize, headY)).ToList();
        var player = new Player(Guid.NewGuid(), name)
        {
            Id = name,
            CurrentPosition = path[^1],
            Direction = new MovementDirection(Direction.Right, At(1, 0)),
            BackgroundColor = new Color("#ff0000", "#ff0000"),
            Path = path,
            SegmentHealth = path.Select(_ => 1.0).ToList(),
            Score = score
        };
        _state.Players.Add(player.Guid, player);
        return player;
    }

    private Obstacle AddShot(IPlayer shooter, double x, double y)
    {
        var shot = new Obstacle(Guid.NewGuid(), HeadSize, _room, shooter)
        {
            InitialPosition = At(x, y),
            CurrentPosition = At(x, y),
            EndPosition = At(5000, 5000),
            Direction = new MovementDirection(Direction.Right, At(1, 0))
        };
        _state.Obstacles.Add(shot.Guid, shot);
        return shot;
    }

    private void AddFood(double x, double y, long value, FoodKind kind = FoodKind.Normal, int credit = 1, string? grants = null)
    {
        var food = new Food(Guid.NewGuid())
        {
            Position = At(x, y), Color = new Color("#00ff00", "#00ff00"), Value = value, Kind = kind, FoodsCredit = credit, GrantsPerk = grants
        };
        _state.Foods.Add(food.Guid, food);
    }

    [Fact]
    public async Task MovePlayer_InitialMass_KeepsLengthAndAdvancesHead()
    {
        var player = AddPlayer("a", 500, 500);

        await _service.MovePlayer(_state, player);

        Assert.Equal(InitialLength, player.Path.Count);
        Assert.Equal(503.75, player.CurrentPosition.X, 3);
        Assert.Equal(503.75, player.Path[^1].X, 3);
    }

    [Fact]
    public async Task MovePlayer_FollowersKeepSpacingWhileTurning()
    {
        var player = AddPlayer("a", 500, 500);
        player.Direction = new MovementDirection(Direction.Up, At(0, 1));

        foreach (var _ in Enumerable.Range(0, 30))
            await _service.MovePlayer(_state, player);

        var gaps = player.Path.Zip(player.Path.Skip(1), (tail, head) => Math.Sqrt(Math.Pow(head.X - tail.X, 2) + Math.Pow(head.Y - tail.Y, 2)));
        Assert.All(gaps, gap => Assert.True(gap <= HeadSize + 1e-6));
    }

    [Fact]
    public async Task MovePlayer_MassGained_GrowsOneSegmentPerMoveUntilTarget()
    {
        var player = AddPlayer("a", 500, 500, score: 2);

        await _service.MovePlayer(_state, player);
        Assert.Equal(InitialLength + 1, player.Path.Count);

        await _service.MovePlayer(_state, player);
        await _service.MovePlayer(_state, player);
        Assert.Equal(InitialLength + 2, player.Path.Count);
        Assert.Equal(player.Path.Count, player.SegmentHealth.Count);
    }

    [Fact]
    public async Task MovePlayer_MassLost_ShrinksTheTail()
    {
        var player = AddPlayer("a", 500, 500, length: 9, score: 0);

        await _service.MovePlayer(_state, player);

        Assert.Equal(InitialLength, player.Path.Count);
        Assert.Equal(InitialLength, player.SegmentHealth.Count);
    }

    [Fact]
    public async Task MovePlayer_Boosting_IsFasterAndSpendsMassOnTheThirtiethTick()
    {
        var player = AddPlayer("a", 500, 500, score: 5);
        player.IsBoosting = true;

        await _service.MovePlayer(_state, player);
        Assert.Equal(500 + 3.75 * 1.8, player.CurrentPosition.X, 3);
        Assert.Equal(5, player.Score);

        foreach (var _ in Enumerable.Range(0, 29))
            await _service.MovePlayer(_state, player);
        Assert.Equal(4, player.Score);
    }

    [Fact]
    public async Task MovePlayer_BoostingWithoutMass_MovesAtNormalSpeed()
    {
        var player = AddPlayer("a", 500, 500, score: 0);
        player.IsBoosting = true;

        await _service.MovePlayer(_state, player);

        Assert.Equal(503.75, player.CurrentPosition.X, 3);
    }

    [Fact]
    public async Task CheckFoodCollision_Pellet_AddsItsValueAndHealsTheMostDamagedSegment()
    {
        var player = AddPlayer("a", 500, 500);
        player.SegmentHealth[1] = 0.2;
        AddFood(500, 500, value: 3);

        await _service.CheckFoodCollision(_state, player);

        Assert.Equal(3, player.Score);
        Assert.Empty(_state.Foods);
        Assert.True(player.SegmentHealth[1] > 0.9);
        Assert.Equal(1.0, player.SegmentHealth[0]);
    }

    [Fact]
    public async Task CheckObstacleCollision_ShotOnBody_DamagesThatSegmentAndConsumesTheShot()
    {
        var target = AddPlayer("target", 500, 500);
        var shooter = AddPlayer("shooter", 1500, 100);
        var shot = AddShot(shooter, target.Path[2].X - 0.1, 500);

        await _service.CheckObstacleCollision(_state, target);

        var expected = 1 - _damage.ShotDamage(InitialLength, InitialLength) / _damage.MaxSegmentHp(InitialLength);
        Assert.Equal(expected, target.SegmentHealth[2], 6);
        Assert.True(target.IsAlive);
        Assert.DoesNotContain(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task CheckObstacleCollision_BiggerShooter_HitsHarder()
    {
        var target = AddPlayer("target", 500, 500);
        var big = AddPlayer("big", 1500, 100, length: 30, score: 24);
        AddShot(big, target.Path[2].X - 0.1, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.True(target.SegmentHealth[2] < 1 - _damage.ShotDamage(InitialLength, InitialLength) / _damage.MaxSegmentHp(InitialLength));
    }

    [Fact]
    public async Task CheckObstacleCollision_LethalHitOnMassBackedSegment_BreaksItAndDropsAPellet()
    {
        var target = AddPlayer("target", 500, 500, length: 8, score: 2);
        target.SegmentHealth[2] = 0.05;
        var brokenAt = target.Path[2];
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, brokenAt.X - 0.1, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.Equal(7, target.Path.Count);
        Assert.Equal(7, target.SegmentHealth.Count);
        Assert.Equal(1, target.Score);
        var pellet = Assert.Single(_state.Foods.Values);
        Assert.Equal(1, pellet.Value);
        Assert.Equal(brokenAt.X, pellet.Position.X);
    }

    [Fact]
    public async Task CheckObstacleCollision_LethalHitOnInitialSegment_StaysAtZeroHp()
    {
        var target = AddPlayer("target", 500, 500, score: 0);
        target.SegmentHealth[2] = 0.01;
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.Path[2].X - 0.1, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.Equal(InitialLength, target.Path.Count);
        Assert.Equal(0, target.SegmentHealth[2]);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public async Task CheckObstacleCollision_ShotOnHead_KillsAndDropsAllMassAsPellets()
    {
        var target = AddPlayer("target", 500, 500, length: 11, score: 5);
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.False(target.IsAlive);
        Assert.Equal(5, _state.Foods.Count);
        Assert.Equal(5, _state.Foods.Values.Sum(food => food.Value));
    }

    [Fact]
    public async Task CheckObstacleCollision_OwnShot_IsIgnored()
    {
        var player = AddPlayer("a", 500, 500);
        var shot = AddShot(player, player.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, player);

        Assert.True(player.IsAlive);
        Assert.Contains(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task CheckObstacleCollision_AlreadyDead_DoesNotDropTwice()
    {
        var target = AddPlayer("target", 500, 500, length: 8, score: 2);
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);
        await _service.CheckObstacleCollision(_state, target);
        var pelletsAfterDeath = _state.Foods.Count;
        AddShot(shooter, target.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.Equal(pelletsAfterDeath, _state.Foods.Count);
    }

    [Fact]
    public async Task ResolveSnakeCollisions_HeadIntoOtherBody_KillsOnlyTheIntruderAndDropsItsMass()
    {
        var wall = AddPlayer("wall", 500, 500, score: 0);
        var intruder = AddPlayer("intruder", 900, 900, length: 8, score: 2);
        intruder.CurrentPosition = At(wall.Path[2].X, 505);
        intruder.Path[^1] = intruder.CurrentPosition;

        await _service.ResolveSnakeCollisions(_state);

        Assert.False(intruder.IsAlive);
        Assert.True(wall.IsAlive);
        Assert.Equal(2, _state.Foods.Values.Sum(food => food.Value));
    }

    [Fact]
    public async Task Shoot_WithMass_FiresOneShotSpendsMassAndStartsTheCooldown()
    {
        var player = AddPlayer("a", 500, 500, score: 3);

        await _service.Shoot(_state, _room, player);

        Assert.Single(_state.Obstacles);
        Assert.Equal(2, player.Score);
        Assert.Equal(15, player.ShotCooldownTicks);
    }

    [Fact]
    public async Task Shoot_WhileCoolingDown_DoesNothing()
    {
        var player = AddPlayer("a", 500, 500, score: 3);
        await _service.Shoot(_state, _room, player);

        await _service.Shoot(_state, _room, player);

        Assert.Single(_state.Obstacles);
        Assert.Equal(2, player.Score);
    }

    [Fact]
    public async Task Shoot_AfterTheCooldownTicksHavePassed_FiresAgain()
    {
        var player = AddPlayer("a", 500, 500, score: 3);
        await _service.Shoot(_state, _room, player);

        foreach (var _ in Enumerable.Range(0, 15))
            await _service.MovePlayer(_state, player);
        await _service.Shoot(_state, _room, player);

        Assert.Equal(2, _state.Obstacles.Count);
        Assert.Equal(1, player.Score);
    }

    [Fact]
    public async Task Shoot_OneTickBeforeTheCooldownEnds_StillDoesNothing()
    {
        var player = AddPlayer("a", 500, 500, score: 3);
        await _service.Shoot(_state, _room, player);

        foreach (var _ in Enumerable.Range(0, 14))
            await _service.MovePlayer(_state, player);
        await _service.Shoot(_state, _room, player);

        Assert.Single(_state.Obstacles);
    }

    [Fact]
    public async Task Shoot_WithoutMass_DoesNothingAndStartsNoCooldown()
    {
        var player = AddPlayer("a", 500, 500, score: 0);

        await _service.Shoot(_state, _room, player);

        Assert.Empty(_state.Obstacles);
        Assert.Equal(0, player.ShotCooldownTicks);
    }

    [Fact]
    public async Task Shoot_DeadPlayer_DoesNothing()
    {
        var player = AddPlayer("a", 500, 500, score: 3);
        player.IsAlive = false;

        await _service.Shoot(_state, _room, player);

        Assert.Empty(_state.Obstacles);
    }

    [Fact]
    public async Task MovePlayer_CooldownNeverGoesBelowZero()
    {
        var player = AddPlayer("a", 500, 500);

        await _service.MovePlayer(_state, player);

        Assert.Equal(0, player.ShotCooldownTicks);
    }

    private void KnowDirection(Direction direction, double x, double y)
        => _knownDirections.Get(direction).Returns((KeyValuePair<Direction, IPointAxis>?)new KeyValuePair<Direction, IPointAxis>(direction, At(x, y)));

    [Fact]
    public void ChangeDirection_Up_SnapsHeadingAndTargetToTheAxisAngle()
    {
        var player = AddPlayer("a", 500, 500);
        _playerService.Get("a").Returns(player);
        KnowDirection(Direction.Up, 0, -1);

        var changed = _service.ChangeDirectionByPlayerId("a", Direction.Up);

        Assert.True(changed);
        Assert.Equal(-Math.PI / 2, player.Heading, 9);
        Assert.Equal(-Math.PI / 2, player.TargetHeading, 9);
    }

    [Fact]
    public async Task ChangeDirection_ThenMove_GoesFullSpeedAtOnce()
    {
        var player = AddPlayer("a", 500, 500);
        _playerService.Get("a").Returns(player);
        KnowDirection(Direction.Up, 0, -1);
        _service.ChangeDirectionByPlayerId("a", Direction.Up);

        await _service.MovePlayer(_state, player);

        Assert.Equal(500, player.CurrentPosition.X, 6);
        Assert.Equal(500 - 3.75, player.CurrentPosition.Y, 3);
    }

    [Fact]
    public void ChangeDirection_Angular_IsRejectedWithoutSideEffects()
    {
        var player = AddPlayer("a", 500, 500);
        player.CurrentDirection = Direction.Right;
        _playerService.Get("a").Returns(player);

        var changed = _service.ChangeDirectionByPlayerId("a", Direction.Angular);

        Assert.False(changed);
        Assert.Equal(Direction.Right, player.CurrentDirection);
    }

    [Fact]
    public void SteerByPlayerId_SetsTheTargetAndMarksTheDirectionAngular()
    {
        var player = AddPlayer("a", 500, 500);
        _playerService.Get("a").Returns(player);

        var steered = _service.SteerByPlayerId("a", 1.0);

        Assert.True(steered);
        Assert.Equal(1.0, player.TargetHeading, 9);
        Assert.Equal(0, player.Heading);
        Assert.Equal(Direction.Angular, player.CurrentDirection);
    }

    [Fact]
    public void SteerByPlayerId_NormalizesTheAngle()
    {
        var player = AddPlayer("a", 500, 500);
        _playerService.Get("a").Returns(player);

        _service.SteerByPlayerId("a", 3 * Math.PI);

        Assert.Equal(Math.PI, player.TargetHeading, 9);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SteerByPlayerId_NonFiniteAngle_IsRejected(double angle)
    {
        var player = AddPlayer("a", 500, 500);
        player.TargetHeading = 0.5;
        _playerService.Get("a").Returns(player);

        Assert.False(_service.SteerByPlayerId("a", angle));
        Assert.Equal(0.5, player.TargetHeading);
    }

    [Fact]
    public void SteerByPlayerId_UnknownPlayer_ReturnsFalse()
    {
        _playerService.Get("nobody").Returns((IPlayer?)null);

        Assert.False(_service.SteerByPlayerId("nobody", 1.0));
    }

    [Fact]
    public async Task MovePlayer_Steering_TurnsAtMostTheTurnRatePerTick()
    {
        var player = AddPlayer("a", 500, 500);
        player.TargetHeading = Math.PI / 2;
        player.CurrentDirection = Direction.Angular;

        await _service.MovePlayer(_state, player);

        var sixDegrees = 6 * Math.PI / 180;
        Assert.Equal(sixDegrees, player.Heading, 3);
        Assert.Equal(Direction.Angular, player.Direction.Direction);
        Assert.Equal(500 + 3.75 * Math.Cos(sixDegrees), player.CurrentPosition.X, 2);
        Assert.Equal(500 + 3.75 * Math.Sin(sixDegrees), player.CurrentPosition.Y, 2);
    }

    [Fact]
    public async Task MovePlayer_Steering_ArrivesOnTheTargetThenKeepsGoingStraight()
    {
        var player = AddPlayer("a", 500, 500);
        player.TargetHeading = Math.PI / 2;
        player.CurrentDirection = Direction.Angular;

        foreach (var _ in Enumerable.Range(0, 20))
            await _service.MovePlayer(_state, player);

        Assert.Equal(Math.PI / 2, player.Heading);
        Assert.Equal(0, player.Direction.Axis.X, 9);
        Assert.Equal(1, player.Direction.Axis.Y, 9);
        var before = player.CurrentPosition.Y;
        await _service.MovePlayer(_state, player);
        Assert.Equal(before + 3.75, player.CurrentPosition.Y, 3);
    }

    [Fact]
    public async Task MovePlayer_Steering_TakesTheShortWayRound()
    {
        var player = AddPlayer("a", 500, 500);
        player.Heading = 170 * Math.PI / 180;
        player.TargetHeading = -170 * Math.PI / 180;
        player.CurrentDirection = Direction.Angular;

        await _service.MovePlayer(_state, player);

        Assert.True(player.Heading > 170 * Math.PI / 180);
    }

    [Fact]
    public async Task MovePlayer_NotSteering_LeavesTheDirectionObjectAlone()
    {
        var player = AddPlayer("a", 500, 500);
        var direction = player.Direction;

        await _service.MovePlayer(_state, player);

        Assert.Same(direction, player.Direction);
    }

    [Fact]
    public async Task Shoot_AfterSteering_TheShotUsesTheCurrentHeadingAxis()
    {
        var player = AddPlayer("a", 500, 500, score: 2);
        player.TargetHeading = 0.5;
        player.CurrentDirection = Direction.Angular;
        await _service.MovePlayer(_state, player);

        await _service.Shoot(_state, _room, player);

        var shot = Assert.Single(_state.Obstacles.Values);
        Assert.Same(player.Direction, shot.Direction);
    }

    [Fact]
    public async Task CreatePlayerInRoom_SpawnHeadingMatchesTheSpawnDirection()
    {
        _knownDirections.Count.Returns(1);
        _knownDirections.Knows(Arg.Any<int>()).Returns(true);
        _knownDirections.Get(Arg.Any<int>()).Returns((KeyValuePair<Direction, IPointAxis>?)new KeyValuePair<Direction, IPointAxis>(Direction.Up, At(0, -1)));
        _roomsService.GetRoom(Arg.Any<ITrackableId>()).Returns(_room);
        _playerRooms.ConnectPlayer(Arg.Any<IPlayer>(), Arg.Any<IRoom?>()).Returns(true);
        _playerService.Create(default!, default!, default!, default!, default!, default).ReturnsForAnyArgs(call =>
            new Player(Guid.NewGuid(), call.ArgAt<string>(1))
            {
                Id = call.ArgAt<string>(0),
                BackgroundColor = call.ArgAt<IColor>(2),
                Direction = call.ArgAt<IMovementDirection>(3),
                CurrentPosition = call.ArgAt<IPointAxis>(4),
                Path = call.ArgAt<IList<IPointAxis>>(5)
            });

        var player = await _service.CreatePlayerInRoom("conn", "ana", _room.Guid);

        Assert.NotNull(player);
        Assert.Equal(-Math.PI / 2, player!.Heading, 9);
        Assert.Equal(-Math.PI / 2, player.TargetHeading, 9);
        Assert.Equal(Direction.Up, player.CurrentDirection);
    }

    [Fact]
    public void SetBoostByPlayerId_KnownPlayer_StoresTheFlag()
    {
        var player = AddPlayer("a", 500, 500);
        _playerService.Get("a").Returns(player);

        var changed = _service.SetBoostByPlayerId("a", true);

        Assert.True(player.IsBoosting);
        Assert.True(changed);
    }

    [Fact]
    public void SetBoostByPlayerId_UnknownPlayer_ReturnsFalse()
        => Assert.False(_service.SetBoostByPlayerId("nobody", true));

    [Fact]
    public async Task CheckFoodCollision_NormalFood_CountsTowardPerks()
    {
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(500, 500, 1);

        await _service.CheckFoodCollision(_state, player);

        Assert.Equal(2, player.FoodsEaten);
        Assert.Equal(new[] { "twin" }, player.Perks);
    }

    [Fact]
    public async Task CheckFoodCollision_Prize_GivesItsMassAndCountsForSeveralFoods()
    {
        var player = AddPlayer("a", 500, 500);
        AddFood(500, 500, value: 20, kind: FoodKind.Prize, credit: 3);

        await _service.CheckFoodCollision(_state, player);

        Assert.Equal(20, player.Score);
        Assert.Equal(3, player.FoodsEaten);
        Assert.Equal(new[] { "twin", "reload" }, player.Perks);
    }

    [Fact]
    public async Task Shoot_WithAnExtraShotPerk_FiresAFanForTheCostOfOne()
    {
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 2;

        await _service.Shoot(_state, _room, player);

        var shots = _state.Obstacles.Values.OrderBy(shot => shot.Direction.Axis.Y).ToArray();
        Assert.Equal(2, shots.Length);
        Assert.Equal(4, player.Score);
        Assert.True(shots[0].Direction.Axis.Y < 0);
        Assert.True(shots[1].Direction.Axis.Y > 0);
        Assert.Equal(-shots[0].Direction.Axis.Y, shots[1].Direction.Axis.Y, 6);
    }

    [Fact]
    public async Task Shoot_WithoutPerks_FiresOneShotStraight()
    {
        var player = AddPlayer("a", 500, 500, score: 5);

        await _service.Shoot(_state, _room, player);

        Assert.Same(player.Direction, Assert.Single(_state.Obstacles.Values).Direction);
    }

    [Fact]
    public async Task Shoot_WithAReloadPerk_CoolsDownFaster()
    {
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 3;

        await _service.Shoot(_state, _room, player);

        Assert.Equal(8, player.ShotCooldownTicks);
    }

    [Fact]
    public async Task MovePlayer_BoostingWithASpeedPerk_MovesFurther()
    {
        var plain = AddPlayer("plain", 500, 500, score: 5);
        var swift = AddPlayer("swift", 500, 700, score: 5);
        swift.FoodsEaten = 4;
        plain.IsBoosting = swift.IsBoosting = true;

        await _service.MovePlayer(_state, plain);
        await _service.MovePlayer(_state, swift);

        var plainDistance = plain.CurrentPosition.X - 500;
        var swiftDistance = swift.CurrentPosition.X - 500;
        Assert.Equal(plainDistance * (1.8 + 0.8) / 1.8, swiftDistance, 3);
    }

    private static PerkRules Only(PerkEffect effect, double amount, int burstIntervalMilliseconds = 80)
        => new(new PerkOptions
        {
            BurstIntervalMilliseconds = burstIntervalMilliseconds,
            Perks = [new() { Id = "p", Name = "P", FoodsRequired = 1, Effect = effect, Amount = amount }]
        });

    private GameStateService With(PerkEffect effect, double amount) => Build(Only(effect, amount));

    [Fact]
    public async Task Shoot_WithBurst_FiresTheFirstShotNowAndTheRestOnTime()
    {
        var service = With(PerkEffect.BurstShot, 2);
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);
        Assert.Single(_state.Obstacles);
        Assert.Equal(4, player.Score);

        for (var tick = 0; tick < 4; tick++) await service.MovePlayer(_state, player);
        Assert.Single(_state.Obstacles);

        await service.MovePlayer(_state, player);
        Assert.Equal(2, _state.Obstacles.Count);

        for (var tick = 0; tick < 5; tick++) await service.MovePlayer(_state, player);
        Assert.Equal(3, _state.Obstacles.Count);
        Assert.Equal(0, player.BurstRemaining);
        Assert.Equal(4, player.Score);
    }

    [Fact]
    public async Task Shoot_WithBurst_TheCooldownCoversTheWholeBurst()
    {
        var service = With(PerkEffect.BurstShot, 2);
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Equal(15 + 2 * 5, player.ShotCooldownTicks);
    }

    [Fact]
    public async Task Shoot_BurstStopsWhenTheSnakeDies()
    {
        var service = With(PerkEffect.BurstShot, 2);
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;
        await service.Shoot(_state, _room, player);

        player.IsAlive = false;
        for (var tick = 0; tick < 12; tick++) await service.MovePlayer(_state, player);

        Assert.Single(_state.Obstacles);
    }

    [Fact]
    public async Task Shoot_WithQuickShot_TheShotIsFaster()
    {
        var service = With(PerkEffect.ShotSpeed, 1.0);
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Equal(2.0, Assert.Single(_state.Obstacles.Values).SpeedFactor);
    }

    [Fact]
    public async Task MoveObstacles_AFasterShotFliesFurther()
    {
        var plain = AddShot(AddPlayer("a", 1500, 100), 100, 700);
        var quick = AddShot(AddPlayer("b", 1500, 300), 100, 800);
        quick.SpeedFactor = 2.0;

        await _service.MoveObstacles(_state);

        Assert.Equal((plain.CurrentPosition.X - 100) * 2, quick.CurrentPosition.X - 100, 6);
    }

    [Fact]
    public async Task Shoot_WithPiercing_TheShotPierces()
    {
        var service = With(PerkEffect.PiercingShot, 1);
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.True(Assert.Single(_state.Obstacles.Values).Piercing);
    }

    [Fact]
    public async Task CheckObstacleCollision_PiercingShot_KeepsFlyingAndHitsASnakeOnce()
    {
        var target = AddPlayer("target", 500, 500);
        var shooter = AddPlayer("shooter", 1500, 100);
        var shot = AddShot(shooter, target.Path[2].X - 0.1, 500);
        shot.Piercing = true;

        await _service.CheckObstacleCollision(_state, target);
        var afterFirst = target.SegmentHealth[2];
        await _service.CheckObstacleCollision(_state, target);

        Assert.True(afterFirst < 1);
        Assert.Equal(afterFirst, target.SegmentHealth[2], 9);
        Assert.Contains(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task CheckObstacleCollision_PiercingShot_StillKillsOnTheHead()
    {
        var target = AddPlayer("target", 500, 500);
        var shooter = AddPlayer("shooter", 1500, 100);
        var shot = AddShot(shooter, target.CurrentPosition.X, 500);
        shot.Piercing = true;

        await _service.CheckObstacleCollision(_state, target);

        Assert.False(target.IsAlive);
        Assert.Contains(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task CheckObstacleCollision_Armor_TakesLessDamage()
    {
        var service = With(PerkEffect.Armor, 0.5);
        var plain = AddPlayer("plain", 500, 500);
        var armored = AddPlayer("armored", 500, 800);
        armored.FoodsEaten = 1;
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, plain.Path[2].X - 0.1, 500);
        AddShot(shooter, armored.Path[2].X - 0.1, 800);

        await service.CheckObstacleCollision(_state, plain);
        await service.CheckObstacleCollision(_state, armored);

        Assert.Equal((1 - plain.SegmentHealth[2]) / 2, 1 - armored.SegmentHealth[2], 6);
    }

    [Fact]
    public async Task MovePlayer_WithRegeneration_HealsTheMostDamagedSegment()
    {
        var service = With(PerkEffect.Regeneration, 30);
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        player.SegmentHealth[1] = 0.5;

        for (var tick = 0; tick < 60; tick++) await service.MovePlayer(_state, player);

        var healedHp = 30 * 60 * _tickRate.TickRate.TotalSeconds;
        Assert.Equal(0.5 + healedHp / _damage.MaxSegmentHp(player.Path.Count), player.SegmentHealth[1], 3);
    }

    [Fact]
    public async Task MovePlayer_WithoutRegeneration_NothingHeals()
    {
        var player = AddPlayer("a", 500, 500);
        player.SegmentHealth[1] = 0.5;

        for (var tick = 0; tick < 60; tick++) await _service.MovePlayer(_state, player);

        Assert.Equal(0.5, player.SegmentHealth[1], 6);
    }

    [Fact]
    public async Task CheckFoodCollision_Magnet_PullsNearbyFoodTowardTheHead()
    {
        var service = With(PerkEffect.Magnet, 100);
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(560, 500, 1);
        AddFood(900, 500, 1);

        await service.CheckFoodCollision(_state, player);

        var xs = _state.Foods.Values.Select(food => food.Position.X).OrderBy(x => x).ToArray();
        Assert.True(xs[0] < 560);
        Assert.True(xs[0] > 500);
        Assert.Equal(900, xs[1]);
    }

    [Fact]
    public async Task CheckFoodCollision_Magnet_LeavesThePrizeWhereItIs()
    {
        var service = With(PerkEffect.Magnet, 100);
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(560, 500, 20, FoodKind.Prize, 8);

        await service.CheckFoodCollision(_state, player);

        Assert.Equal(560, Assert.Single(_state.Foods.Values).Position.X);
    }

    [Fact]
    public async Task CheckFoodCollision_Magnet_EventuallyDeliversTheFood()
    {
        var service = With(PerkEffect.Magnet, 100);
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(560, 500, 1);

        for (var tick = 0; tick < 30; tick++) await service.CheckFoodCollision(_state, player);

        Assert.Empty(_state.Foods);
    }

    [Fact]
    public async Task CheckFoodCollision_EarningAutoFire_TurnsItOn()
    {
        var service = Build(new PerkRules(new PerkOptions
        {
            Perks = [new() { Id = "a", Name = "A", FoodsRequired = 2, Effect = PerkEffect.AutoFire, Amount = 1 }]
        }));
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(500, 500, 1);

        await service.CheckFoodCollision(_state, player);

        Assert.True(player.AutoFire);
    }

    [Fact]
    public async Task CheckObstacleCollision_ShotOnBody_RecordsTheDamageForThePlayersInvolved()
    {
        var target = AddPlayer("target", 500, 500);
        var shooter = AddPlayer("shooter", 1500, 100);
        var hitAt = target.Path[2];
        AddShot(shooter, hitAt.X - 0.1, 500);

        await _service.CheckObstacleCollision(_state, target);

        var hit = Assert.Single(_state.Hits);
        Assert.Equal(shooter.Guid, hit.ShooterGuid);
        Assert.Equal(target.Guid, hit.TargetGuid);
        Assert.Equal(DamageKind.Hit, hit.Kind);
        Assert.Equal(_damage.ShotDamage(InitialLength, InitialLength), hit.Damage, 6);
        Assert.Equal(hitAt.X, hit.X);
        Assert.Equal(hitAt.Y, hit.Y);
    }

    [Fact]
    public async Task CheckObstacleCollision_ArmoredTarget_RecordsTheDamageItReallyTook()
    {
        var service = With(PerkEffect.Armor, 0.5);
        var target = AddPlayer("target", 500, 500);
        target.FoodsEaten = 1;
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.Path[2].X - 0.1, 500);

        await service.CheckObstacleCollision(_state, target);

        Assert.Equal(_damage.ShotDamage(InitialLength, InitialLength) / 2, Assert.Single(_state.Hits).Damage, 6);
    }

    [Fact]
    public async Task CheckObstacleCollision_SegmentWithLittleHpLeft_RecordsOnlyWhatWasLeft()
    {
        var target = AddPlayer("target", 500, 500, length: 8, score: 2);
        target.SegmentHealth[2] = 0.05;
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.Path[2].X - 0.1, 500);

        await _service.CheckObstacleCollision(_state, target);

        var hit = Assert.Single(_state.Hits);
        Assert.Equal(DamageKind.Broke, hit.Kind);
        Assert.Equal(0.05 * _damage.MaxSegmentHp(8), hit.Damage, 6);
    }

    [Fact]
    public async Task CheckObstacleCollision_ShotOnTheHead_RecordsAKill()
    {
        var target = AddPlayer("target", 500, 500);
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, target);

        var hit = Assert.Single(_state.Hits);
        Assert.Equal(DamageKind.Kill, hit.Kind);
        Assert.Equal(shooter.Guid, hit.ShooterGuid);
    }

    [Fact]
    public async Task CheckObstacleCollision_NoShot_RecordsNothing()
    {
        var target = AddPlayer("target", 500, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.Empty(_state.Hits);
    }

    [Fact]
    public async Task CheckObstacleCollision_PiercingShotPassingAgain_RecordsOnce()
    {
        var target = AddPlayer("target", 500, 500);
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.Path[2].X - 0.1, 500).Piercing = true;

        await _service.CheckObstacleCollision(_state, target);
        await _service.CheckObstacleCollision(_state, target);

        Assert.Single(_state.Hits);
    }

    private GameStateService WithScript(string script, params string[] rooms)
        => Build(new PerkRules(new PerkOptions
        {
            Perks = [new() { Id = "scripted", Name = "Scripted", FoodsRequired = 1, Script = script, Rooms = rooms.ToList() }]
        }));

    [Fact]
    public async Task Script_GivesExtraShots_InTheRoomItNames()
    {
        var service = WithScript("function modify() { return { extraShots: 1 }; }", _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Equal(2, _state.Obstacles.Count);
    }

    [Fact]
    public async Task Script_DoesNothing_InAnotherRoom()
    {
        var service = WithScript("function modify() { return { extraShots: 3 }; }", Guid.NewGuid().ToString());
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Single(_state.Obstacles);
        Assert.Empty(player.Perks);
    }

    [Fact]
    public async Task Script_DoesNothing_WhenNoRoomIsNamed()
    {
        var service = WithScript("function modify() { return { extraShots: 3 }; }");
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Single(_state.Obstacles);
    }

    [Fact]
    public async Task Script_SeesTheSnakeAndTheRoom()
    {
        var service = WithScript(
            "function modify(snake, room) { return { extraShots: snake.length === 6 && snake.mass === 5 && room.players === 2 ? 1 : 0 }; }",
            _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500, score: 5);
        AddPlayer("b", 1500, 900);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Equal(2, _state.Obstacles.Count);
    }

    [Fact]
    public async Task Script_IsAskedAgainOnlyAfterAShortWhile()
    {
        var service = WithScript("function modify(snake) { return { extraShots: snake.mass >= 10 ? 1 : 0 }; }", _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;
        await service.Shoot(_state, _room, player);
        Assert.Single(_state.Obstacles);

        player.Score = 20;
        player.ShotCooldownTicks = 0;
        await service.Shoot(_state, _room, player);
        Assert.Equal(2, _state.Obstacles.Count);

        _clock.Advance(TimeSpan.FromMilliseconds(400));
        player.ShotCooldownTicks = 0;
        await service.Shoot(_state, _room, player);
        Assert.Equal(4, _state.Obstacles.Count);
    }

    [Fact]
    public async Task Script_OnEat_GivesMass()
    {
        var service = WithScript("function onEat(snake, food) { return { mass: 4 }; }", _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(500, 500, 1);

        await service.CheckFoodCollision(_state, player);

        Assert.Equal(1 + 4, player.Score);
    }

    [Fact]
    public async Task Script_OnEat_HealsTheMostDamagedSegment()
    {
        var service = WithScript("function onEat() { return { heal: 20 }; }", _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        player.SegmentHealth[1] = 0.5;
        AddFood(500, 500, 0 + 1);
        var plain = AddPlayer("plain", 500, 900);
        plain.FoodsEaten = 1;
        plain.SegmentHealth[1] = 0.5;
        AddFood(500, 900, 1);

        await service.CheckFoodCollision(_state, player);
        await service.CheckFoodCollision(_state, plain);

        Assert.True(player.SegmentHealth[1] > plain.SegmentHealth[1] - 1e-9);
        Assert.Equal(0.5 + 20 / _damage.MaxSegmentHp(player.Path.Count) + _damage.HealAmount(1) / _damage.MaxSegmentHp(player.Path.Count), player.SegmentHealth[1], 4);
    }

    [Fact]
    public async Task Script_OnEat_DoesNothingInAnotherRoom()
    {
        var service = WithScript("function onEat() { return { mass: 40 }; }", Guid.NewGuid().ToString());
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 1;
        AddFood(500, 500, 1);

        await service.CheckFoodCollision(_state, player);

        Assert.Equal(1, player.Score);
    }

    [Fact]
    public async Task Script_ThatBreaksDownCannotStopTheGame()
    {
        var service = WithScript("function modify(snake) { if (snake.name === 'a') { while (true) {} } return {}; }", _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500, score: 5);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);
        await service.MovePlayer(_state, player);

        Assert.Single(_state.Obstacles);
        Assert.True(player.IsAlive);
    }

    [Fact]
    public async Task Script_AnotherSnakesScriptEffectApplies_ToItsOwnArmor()
    {
        var service = WithScript("function modify() { return { damageFactor: 0.5 }; }", _room.Guid.ToString());
        var plain = AddPlayer("plain", 500, 500);
        var armored = AddPlayer("armored", 500, 800);
        armored.FoodsEaten = 1;
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, plain.Path[2].X - 0.1, 500);
        AddShot(shooter, armored.Path[2].X - 0.1, 800);

        await service.CheckObstacleCollision(_state, plain);
        await service.CheckObstacleCollision(_state, armored);

        Assert.Equal((1 - plain.SegmentHealth[2]) / 2, 1 - armored.SegmentHealth[2], 6);
    }

    [Fact]
    public async Task CheckFoodCollision_APrizeThatGrantsAPerk_GivesItWhateverTheFoods()
    {
        var player = AddPlayer("a", 500, 500, score: 5);
        AddFood(500, 500, 10, FoodKind.Prize, credit: 0, grants: "twin");

        await _service.CheckFoodCollision(_state, player);

        Assert.Contains("twin", player.GrantedPerks);
        Assert.Contains("twin", player.Perks);
        Assert.Equal(0, player.FoodsEaten);
        await _service.Shoot(_state, _room, player);
        Assert.Equal(2, _state.Obstacles.Count);
    }

    [Fact]
    public async Task CheckFoodCollision_APerkIsGrantedOnce()
    {
        var player = AddPlayer("a", 500, 500);
        AddFood(500, 500, 1, FoodKind.Prize, credit: 0, grants: "twin");
        await _service.CheckFoodCollision(_state, player);
        AddFood(500, 500, 1, FoodKind.Prize, credit: 0, grants: "twin");

        await _service.CheckFoodCollision(_state, player);

        Assert.Single(player.GrantedPerks);
    }

    [Fact]
    public async Task CheckFoodCollision_APrizeWithoutAGrantGivesNoPerk()
    {
        var player = AddPlayer("a", 500, 500);
        AddFood(500, 500, 1, FoodKind.Prize, credit: 0);

        await _service.CheckFoodCollision(_state, player);

        Assert.Empty(player.GrantedPerks);
    }

    [Fact]
    public async Task Script_FreeShots_CostNoMass_AndWorkAtZeroMass()
    {
        var service = WithScript("function modify() { return { freeShots: true }; }", _room.Guid.ToString());
        var player = AddPlayer("a", 500, 500, score: 0);
        player.FoodsEaten = 1;

        await service.Shoot(_state, _room, player);

        Assert.Single(_state.Obstacles);
        Assert.Equal(0, player.Score);
    }

    [Fact]
    public async Task Script_Invulnerable_ASnakeShotInTheHeadLives_AndTheShotIsSpent()
    {
        var service = WithScript("function modify() { return { invulnerable: true }; }", _room.Guid.ToString());
        var target = AddPlayer("target", 500, 500, length: 11, score: 5);
        target.FoodsEaten = 1;
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await service.CheckObstacleCollision(_state, target);

        Assert.True(target.IsAlive);
        Assert.Empty(_state.Obstacles);
        Assert.Empty(_state.Hits);
    }

    [Fact]
    public async Task Script_Invulnerable_ASnakeThatRunsIntoOthersLives()
    {
        var service = WithScript("function modify() { return { invulnerable: true }; }", _room.Guid.ToString());
        var wall = AddPlayer("wall", 500, 500);
        var intruder = AddPlayer("intruder", 900, 900, length: 8);
        intruder.FoodsEaten = 1;
        intruder.CurrentPosition = At(wall.Path[2].X, 505);
        intruder.Path[^1] = intruder.CurrentPosition;

        await service.ResolveSnakeCollisions(_state);

        Assert.True(intruder.IsAlive);
    }

    [Fact]
    public async Task Script_OnKill_RunsForTheSnakeThatShotAndGivesWhatItReturns()
    {
        var service = WithScript("function onKill(snake, victim) { return { mass: 5 }; }", _room.Guid.ToString());
        var target = AddPlayer("target", 500, 500, length: 11);
        var shooter = AddPlayer("shooter", 1500, 100, score: 1);
        shooter.FoodsEaten = 1;
        AddShot(shooter, target.CurrentPosition.X, 500);

        await service.CheckObstacleCollision(_state, target);

        Assert.False(target.IsAlive);
        Assert.Equal(6, shooter.Score);
    }

    [Fact]
    public async Task Script_OnKill_RunsForTheSnakeThatWasRunInto()
    {
        var service = WithScript("function onKill(snake, victim) { return { mass: 5 }; }", _room.Guid.ToString());
        var wall = AddPlayer("wall", 500, 500, score: 1);
        wall.FoodsEaten = 1;
        var intruder = AddPlayer("intruder", 900, 900, length: 8);
        intruder.CurrentPosition = At(wall.Path[2].X, 505);
        intruder.Path[^1] = intruder.CurrentPosition;

        await service.ResolveSnakeCollisions(_state);

        Assert.False(intruder.IsAlive);
        Assert.Equal(6, wall.Score);
    }

    [Fact]
    public async Task Script_KillsInARow_GiveInvulnerabilityForAWhile()
    {
        const string streak = """
            function onKill(snake) {
              snake.state.kills = (snake.state.kills || 0) + 1;
              if (snake.state.kills >= 2) snake.state.until = snake.now + 5000;
            }
            function modify(snake) { return { invulnerable: snake.now < (snake.state.until || 0) }; }
            """;
        var service = WithScript(streak, _room.Guid.ToString());
        var hero = AddPlayer("hero", 1500, 100, score: 1);
        hero.FoodsEaten = 1;
        for (var kill = 0; kill < 2; kill++)
        {
            var victim = AddPlayer($"victim{kill}", 500, 300 + kill * 200, length: 11);
            AddShot(hero, victim.CurrentPosition.X, victim.CurrentPosition.Y);
            await service.CheckObstacleCollision(_state, victim);
        }
        var outsider = AddPlayer("outsider", 1500, 900);
        AddShot(outsider, hero.CurrentPosition.X, hero.CurrentPosition.Y);

        await service.CheckObstacleCollision(_state, hero);
        Assert.True(hero.IsAlive);

        _clock.Advance(TimeSpan.FromSeconds(6));
        AddShot(outsider, hero.CurrentPosition.X, hero.CurrentPosition.Y);
        await service.CheckObstacleCollision(_state, hero);
        Assert.False(hero.IsAlive);
    }

    // StrikeArea: a bolt of lightning on a spot of the map.

    private static double Middle(double position) => position + HeadSize / 2.0;

    [Fact]
    public void StrikeArea_ASegmentInsideTheRadius_BreaksOffAndDropsItsMassAsAPellet()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        var tail = victim.Path[0];

        var died = _service.StrikeArea(_state, Middle(tail.X), Middle(tail.Y), 5);

        Assert.Empty(died);
        Assert.True(victim.IsAlive);
        Assert.Equal(10, victim.Path.Count);
        Assert.Equal(10, victim.SegmentHealth.Count);
        Assert.Equal(9, victim.Score);
        var pellet = Assert.Single(_state.Foods.Values);
        Assert.Equal((tail.X, tail.Y, 1L), (pellet.Position.X, pellet.Position.Y, pellet.Value));
    }

    [Fact]
    public void StrikeArea_SeveralSegments_EachDropsAPellet()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);

        _service.StrikeArea(_state, Middle(victim.Path[1].X), Middle(500), HeadSize * 1.2);

        Assert.Equal(8, victim.Path.Count);
        Assert.Equal(3, _state.Foods.Count);
        Assert.Equal(7, victim.Score);
    }

    [Fact]
    public void StrikeArea_TheHead_KillsTheSnakeAndDropsItsMass()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);

        var died = _service.StrikeArea(_state, Middle(500), Middle(500), 5);

        Assert.Same(victim, Assert.Single(died));
        Assert.False(victim.IsAlive);
        Assert.Equal(10, _state.Foods.Values.Sum(food => food.Value));
    }

    [Fact]
    public void StrikeArea_AWholeSnakeInsideTheRadius_DiesOnce()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);

        var died = _service.StrikeArea(_state, 450, 500, 500);

        Assert.Same(victim, Assert.Single(died));
        Assert.Equal(10, _state.Foods.Values.Sum(food => food.Value));
    }

    [Fact]
    public void StrikeArea_NobodyInTheRadius_ChangesNothing()
    {
        var other = AddPlayer("other", 500, 500, length: 11, score: 10);

        var died = _service.StrikeArea(_state, 1500, 900, 100);

        Assert.Empty(died);
        Assert.Equal(11, other.Path.Count);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public void StrikeArea_ASnakeAtItsStartingLength_KeepsItsBody()
    {
        var small = AddPlayer("small", 500, 500, length: InitialLength, score: 5);

        _service.StrikeArea(_state, Middle(small.Path[0].X), Middle(500), 5);

        Assert.Equal(InitialLength, small.Path.Count);
        Assert.Empty(_state.Foods);
        Assert.Equal(5, small.Score);
    }

    [Fact]
    public void StrikeArea_AnInvulnerableSnake_IsUntouched()
    {
        var service = WithScript("function modify() { return { invulnerable: true }; }", _room.Guid.ToString());
        var shielded = AddPlayer("shielded", 500, 500, length: 11, score: 10);
        shielded.FoodsEaten = 1;

        var died = service.StrikeArea(_state, 450, 500, 500);

        Assert.Empty(died);
        Assert.True(shielded.IsAlive);
        Assert.Equal(11, shielded.Path.Count);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public void StrikeArea_ASnakeAlreadyDead_IsLeftAlone()
    {
        var dead = AddPlayer("dead", 500, 500, length: 11, score: 10);
        dead.IsAlive = false;

        var died = _service.StrikeArea(_state, 450, 500, 500);

        Assert.Empty(died);
        Assert.Empty(_state.Foods);
    }

    // The turret: a boss that fires volleys and is shot down.

    private WorldBoss AddBoss(double x = 800, double y = 400, int hp = 3, long reward = 20, string? perk = null)
    {
        var boss = new WorldBoss
        {
            Id = Guid.NewGuid(), X = x, Y = y, Size = 60, Hp = hp, MaxHp = hp, Arms = 4, ShotSpeed = 0.5, RewardMass = reward, GrantsPerk = perk
        };
        _state.Boss = boss;
        return boss;
    }

    private Obstacle AddBossShot(WorldBoss boss, double x, double y)
    {
        var shot = new Obstacle(Guid.NewGuid(), HeadSize, _room, new TrackableId(boss.Id))
        {
            InitialPosition = At(x, y),
            CurrentPosition = At(x, y),
            EndPosition = At(5000, 5000),
            Direction = new MovementDirection(Direction.Right, At(1, 0))
        };
        _state.Obstacles.Add(shot.Guid, shot);
        return shot;
    }

    [Fact]
    public void FireTurret_OneShotPerAngle_OwnedByTheBossAndSlow()
    {
        var boss = AddBoss();

        _service.FireTurret(_state, [0, Math.PI / 2]);

        Assert.Equal(2, _state.Obstacles.Count);
        Assert.All(_state.Obstacles.Values, shot =>
        {
            Assert.Equal(boss.Id, shot.CreatedyBy.Guid);
            Assert.Equal(0.5, shot.SpeedFactor);
        });
        var axes = _state.Obstacles.Values.Select(shot => (Math.Round(shot.Direction.Axis.X, 6), Math.Round(shot.Direction.Axis.Y, 6))).ToList();
        Assert.Contains((1.0, 0.0), axes);
        Assert.Contains((0.0, 1.0), axes);
    }

    [Fact]
    public void FireTurret_ShotsStartInTheMiddleOfTheTurret()
    {
        AddBoss(800, 400);

        _service.FireTurret(_state, [0]);

        var shot = Assert.Single(_state.Obstacles.Values);
        Assert.Equal(800 - HeadSize / 2.0, shot.CurrentPosition.X, 3);
        Assert.Equal(400 - HeadSize / 2.0, shot.CurrentPosition.Y, 3);
    }

    [Fact]
    public void FireTurret_WithoutABoss_DoesNothing()
    {
        _service.FireTurret(_state, [0, 1]);

        Assert.Empty(_state.Obstacles);
    }

    [Fact]
    public async Task ResolveBossHits_AShotThatReachesIt_TakesOneHitPointAndIsSpent()
    {
        var boss = AddBoss(800, 400);
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 810, 410);

        await _service.ResolveBossHits(_state);

        Assert.Equal(2, boss.Hp);
        Assert.Empty(_state.Obstacles);
        Assert.Equal(shooter.Guid, boss.LastHitBy);
        var hit = Assert.Single(_state.Hits);
        Assert.Equal((shooter.Guid, boss.Id, DamageKind.Hit), (hit.ShooterGuid, hit.TargetGuid, hit.Kind));
        Assert.Equal(1, hit.Damage);
    }

    [Fact]
    public async Task ResolveBossHits_AShotThatMisses_ChangesNothing()
    {
        var boss = AddBoss(800, 400);
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 200, 410);

        await _service.ResolveBossHits(_state);

        Assert.Equal(3, boss.Hp);
        Assert.Single(_state.Obstacles);
        Assert.Empty(_state.Hits);
    }

    [Fact]
    public async Task ResolveBossHits_ItsOwnShots_NeverHurtIt()
    {
        var boss = AddBoss(800, 400);
        AddBossShot(boss, 800, 400);

        await _service.ResolveBossHits(_state);

        Assert.Equal(3, boss.Hp);
        Assert.Single(_state.Obstacles);
    }

    [Fact]
    public async Task ResolveBossHits_APiercingShot_HitsItOnceAndGoesOn()
    {
        var boss = AddBoss(800, 400);
        var shooter = AddPlayer("shooter", 100, 100);
        var shot = AddShot(shooter, 810, 410);
        shot.Piercing = true;

        await _service.ResolveBossHits(_state);
        await _service.ResolveBossHits(_state);

        Assert.Equal(2, boss.Hp);
        Assert.Single(_state.Obstacles);
    }

    [Fact]
    public async Task ResolveBossHits_TheLastHitPoint_BringsItDownAndDropsTheRewardAsFood()
    {
        var boss = AddBoss(800, 400, hp: 1, reward: 20);
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 810, 410);

        await _service.ResolveBossHits(_state);

        Assert.Null(_state.Boss);
        Assert.Equal(20, _state.Foods.Values.Sum(food => food.Value));
        Assert.All(_state.Foods.Values, food => Assert.InRange(Math.Abs(food.Position.X - 800), 0, 100));
        Assert.Equal(DamageKind.Kill, Assert.Single(_state.Hits).Kind);
    }

    [Fact]
    public async Task ResolveBossHits_TheSnakeThatLandsTheLastShot_GetsThePerk()
    {
        AddBoss(800, 400, hp: 1, perk: "twin");
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 810, 410);

        await _service.ResolveBossHits(_state);

        Assert.Contains("twin", shooter.GrantedPerks);
        Assert.Contains("twin", shooter.Perks);
    }

    [Fact]
    public async Task ResolveBossHits_NoRewardMass_DropsNoFood()
    {
        AddBoss(800, 400, hp: 1, reward: 0);
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 810, 410);

        await _service.ResolveBossHits(_state);

        Assert.Null(_state.Boss);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public async Task ResolveBossHits_WithoutABoss_DoesNothing()
    {
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 810, 410);

        await _service.ResolveBossHits(_state);

        Assert.Single(_state.Obstacles);
    }

    [Fact]
    public async Task CheckObstacleCollision_AShotFromTheBoss_HurtsASnakeLikeAnyOther()
    {
        var boss = AddBoss();
        var target = AddPlayer("target", 500, 500, length: 11, score: 5);
        AddBossShot(boss, target.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.False(target.IsAlive);
        Assert.Empty(_state.Obstacles);
    }

    // ApplyZoneDamage: snakes outside the safe zone pay.

    [Fact]
    public void ApplyZoneDamage_ASnakeOutsideLosesMass_ThatFallsAsFoodWhereItIs()
    {
        var outside = AddPlayer("outside", 1500, 900, score: 10);

        var died = _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Empty(died);
        Assert.True(outside.IsAlive);
        Assert.Equal(6, outside.Score);
        var pellet = Assert.Single(_state.Foods.Values);
        Assert.Equal(4, pellet.Value);
        Assert.InRange(Math.Abs(pellet.Position.X - 1500), 0, HeadSize * 2);
        Assert.InRange(Math.Abs(pellet.Position.Y - 900), 0, HeadSize * 2);
    }

    [Fact]
    public void ApplyZoneDamage_ASnakeInsideIsUntouched()
    {
        var inside = AddPlayer("inside", 300, 300, score: 10);

        var died = _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Empty(died);
        Assert.Equal(10, inside.Score);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public void ApplyZoneDamage_ASnakeWithLessMassThanTheDamage_LosesWhatItHas()
    {
        var poor = AddPlayer("poor", 1500, 900, score: 3);

        var died = _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Empty(died);
        Assert.Equal(0, poor.Score);
        Assert.Equal(3, Assert.Single(_state.Foods.Values).Value);
    }

    [Fact]
    public void ApplyZoneDamage_ASnakeWithNoMassLeft_Dies()
    {
        var broke = AddPlayer("broke", 1500, 900, score: 0);

        var died = _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Same(broke, Assert.Single(died));
        Assert.False(broke.IsAlive);
    }

    [Fact]
    public void ApplyZoneDamage_CountsTheShortWayRoundTheWorld()
    {
        // The room is 1920 wide: a head at 1900 is 40 from a centre at 20.
        var near = AddPlayer("near", 1900, 500, score: 10);

        _service.ApplyZoneDamage(_state, 20, 500, 100, 4);

        Assert.Equal(10, near.Score);
    }

    [Fact]
    public void ApplyZoneDamage_AnInvulnerableSnake_IsUntouched()
    {
        var service = WithScript("function modify() { return { invulnerable: true }; }", _room.Guid.ToString());
        var shielded = AddPlayer("shielded", 1500, 900, score: 0);
        shielded.FoodsEaten = 1;

        var died = service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Empty(died);
        Assert.True(shielded.IsAlive);
    }

    [Fact]
    public void ApplyZoneDamage_ASnakeAlreadyDead_IsLeftAlone()
    {
        var dead = AddPlayer("dead", 1500, 900, score: 10);
        dead.IsAlive = false;

        var died = _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Empty(died);
        Assert.Equal(10, dead.Score);
    }

    // The snake that is struck or left outside the zone is told how much mass it lost, at its head.

    [Fact]
    public void StrikeArea_TellsTheSnakeHowMuchMassTheStrikeCost_AtItsHead()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);

        _service.StrikeArea(_state, Middle(victim.Path[1].X), Middle(500), HeadSize * 1.2);

        var lost = Assert.Single(_state.Hits, hit => hit.Kind == DamageKind.Lost);
        Assert.Equal((Guid.Empty, victim.Guid, 3.0), (lost.ShooterGuid, lost.TargetGuid, lost.Damage));
        Assert.Equal((victim.CurrentPosition.X, victim.CurrentPosition.Y), (lost.X, lost.Y));
    }

    [Fact]
    public void StrikeArea_AHeadHit_TellsTheSnakeItLostItsWholeMass()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);

        _service.StrikeArea(_state, Middle(500), Middle(500), 5);

        var lost = Assert.Single(_state.Hits, hit => hit.Kind == DamageKind.Lost);
        Assert.Equal((victim.Guid, 10.0), (lost.TargetGuid, lost.Damage));
    }

    [Fact]
    public void StrikeArea_NothingHit_TellsNobody()
    {
        AddPlayer("other", 500, 500, length: 11, score: 10);

        _service.StrikeArea(_state, 1500, 900, 100);

        Assert.Empty(_state.Hits);
    }

    [Fact]
    public void ApplyZoneDamage_TellsTheSnakeHowMuchItLost_AtItsHead()
    {
        var outside = AddPlayer("outside", 1500, 900, score: 10);

        _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        var lost = Assert.Single(_state.Hits);
        Assert.Equal((DamageKind.Lost, outside.Guid, 4.0), (lost.Kind, lost.TargetGuid, lost.Damage));
        Assert.Equal(Guid.Empty, lost.ShooterGuid);
        Assert.Equal((outside.CurrentPosition.X, outside.CurrentPosition.Y), (lost.X, lost.Y));
    }

    [Fact]
    public void ApplyZoneDamage_ASnakeInsideOrOneThatDies_GetsNoLossNotice()
    {
        AddPlayer("inside", 300, 300, score: 10);
        AddPlayer("broke", 1500, 900, score: 0);

        _service.ApplyZoneDamage(_state, 300, 300, 100, 4);

        Assert.Empty(_state.Hits);
    }

    // Perk tiers, and what kills and bosses are worth toward them.

    private GameStateService WithTiers(int killCredit = 3)
        => Build(new PerkRules(new PerkOptions
        {
            KillFoodsCredit = killCredit,
            Perks =
            [
                new()
                {
                    Id = "magnet", Name = "Magnet", FoodsRequired = 2, Effect = PerkEffect.Magnet, Amount = 60,
                    Tiers = [new PerkTier { FoodsRequired = 6, Multiplier = 2 }]
                }
            ]
        }));

    [Fact]
    public async Task CheckFoodCollision_TellsWhichTierEachPerkIsAt()
    {
        var service = WithTiers();
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 5;
        AddFood(player.CurrentPosition.X, player.CurrentPosition.Y, 1);

        await service.CheckFoodCollision(_state, player);

        Assert.Equal(["magnet"], player.Perks);
        Assert.Equal([2], player.PerkTiers);
    }

    [Fact]
    public async Task CheckFoodCollision_BelowTheSecondTier_IsTheFirst()
    {
        var service = WithTiers();
        var player = AddPlayer("a", 500, 500);
        player.FoodsEaten = 2;
        AddFood(player.CurrentPosition.X, player.CurrentPosition.Y, 1);

        await service.CheckFoodCollision(_state, player);

        Assert.Equal([1], player.PerkTiers);
    }

    [Fact]
    public async Task ResolveSnakeCollisions_TheSnakeThatWasRunIntoGetsCreditForTheKill_TowardItsPerks()
    {
        var service = WithTiers(killCredit: 4);
        var wall = AddPlayer("wall", 500, 500, score: 0);
        wall.FoodsEaten = 3;
        var intruder = AddPlayer("intruder", 900, 900, length: 8, score: 2);
        intruder.CurrentPosition = At(wall.Path[2].X, 505);
        intruder.Path[^1] = intruder.CurrentPosition;

        await service.ResolveSnakeCollisions(_state);

        Assert.Equal(7, wall.FoodsEaten);
        Assert.Equal([2], wall.PerkTiers);
        Assert.Equal(["magnet"], wall.Perks);
    }

    [Fact]
    public async Task CheckObstacleCollision_ShootingAHeadAlsoGivesCreditForTheKill()
    {
        var service = WithTiers(killCredit: 4);
        var target = AddPlayer("target", 500, 500, length: 11, score: 5);
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await service.CheckObstacleCollision(_state, target);

        Assert.Equal(4, shooter.FoodsEaten);
    }

    [Fact]
    public async Task ResolveSnakeCollisions_NoCreditForAKillOfItself()
    {
        var service = WithTiers(killCredit: 4);
        var snake = AddPlayer("snake", 500, 500, length: 8, score: 2);
        snake.CurrentPosition = At(snake.Path[2].X, 505);
        snake.Path[^1] = snake.CurrentPosition;

        await service.ResolveSnakeCollisions(_state);

        Assert.Equal(0, snake.FoodsEaten);
    }

    [Fact]
    public async Task ResolveBossHits_TheSnakeThatBringsTheBossDown_GetsItsCreditTowardItsPerks()
    {
        var service = WithTiers();
        var boss = AddBoss(800, 400, hp: 1);
        var shooter = AddPlayer("shooter", 100, 100);
        shooter.FoodsEaten = 1;
        AddShot(shooter, 810, 410);
        _state.Boss = new WorldBoss { Id = boss.Id, X = 800, Y = 400, Size = 60, Hp = 1, MaxHp = 1, Arms = 4, FoodsCredit = 6 };

        await service.ResolveBossHits(_state);

        Assert.Equal(7, shooter.FoodsEaten);
        Assert.Equal([2], shooter.PerkTiers);
    }

    [Fact]
    public async Task ResolveBossHits_ABossThatIsOnlyHurt_GivesNoCredit()
    {
        var service = WithTiers();
        _state.Boss = new WorldBoss { Id = Guid.NewGuid(), X = 800, Y = 400, Size = 60, Hp = 3, MaxHp = 3, Arms = 4, FoodsCredit = 6 };
        var shooter = AddPlayer("shooter", 100, 100);
        AddShot(shooter, 810, 410);

        await service.ResolveBossHits(_state);

        Assert.Equal(0, shooter.FoodsEaten);
    }

    // Stats of signed-in players: counted for each life, reported once when the player is removed (docs/ACHIEVEMENTS.md).

    private static readonly Guid AccountA = Guid.NewGuid();
    private static readonly Guid AccountB = Guid.NewGuid();

    private sealed record Reported(Guid AccountId, LifeStatsReport Report);

    private (GameStateService Service, List<Reported> Reports) Reporting()
    {
        var reporter = Substitute.For<IStatsReporter>();
        var reports = new List<Reported>();
        reporter.When(sent => sent.Report(Arg.Any<Guid>(), Arg.Any<LifeStatsReport>()))
            .Do(call => reports.Add(new Reported(call.ArgAt<Guid>(0), call.ArgAt<LifeStatsReport>(1))));
        return (Build(_perks, reporter), reports);
    }

    private Player AddAccountPlayer(GameStateService service, Guid accountId, string name, double headX, double headY, int length = InitialLength, long score = 0)
    {
        var player = AddPlayer(name, headX, headY, length, score);
        service.AttachAccount(player, accountId);
        return player;
    }

    private static LifeStatsReport ReportOf(List<Reported> reports, Guid accountId) => Assert.Single(reports, sent => sent.AccountId == accountId).Report;

    [Fact]
    public void AttachAccount_TagsThePlayerWithItsAccount()
    {
        var (service, _) = Reporting();

        var player = AddAccountPlayer(service, AccountA, "vini", 500, 500);

        Assert.Equal(AccountA, player.AccountId);
    }

    [Fact]
    public async Task DisconnectPlayer_AnAccountPlayerLeavingAlive_ReportsOneGameAndItsPlayTime()
    {
        var (service, reports) = Reporting();
        var player = AddAccountPlayer(service, AccountA, "vini", 500, 500, score: 4);
        _clock.Advance(TimeSpan.FromSeconds(90.5));

        await service.DisconnectPlayer(_room, player);

        var report = ReportOf(reports, AccountA);
        Assert.Equal((1L, 0L, 90L), (report.Counters[StatKeys.Games], report.Counters[StatKeys.Deaths], report.Counters[StatKeys.PlaySeconds]));
        Assert.Equal(4, report.Maxima[StatKeys.BestScore]);
    }

    [Fact]
    public async Task DisconnectPlayer_TwiceForTheSameLife_ReportsItOnce()
    {
        var (service, reports) = Reporting();
        var player = AddAccountPlayer(service, AccountA, "vini", 500, 500);

        await service.DisconnectPlayer(_room, player);
        _state.Players.Add(player.Guid, player);
        await service.DisconnectPlayer(_room, player);

        Assert.Single(reports);
    }

    [Fact]
    public async Task DisconnectPlayer_AGuest_ReportsNothing()
    {
        var (service, reports) = Reporting();
        var guest = AddPlayer("guest", 500, 500, score: 3);
        AddFood(500, 500, 2);
        await service.CheckFoodCollision(_state, guest);

        await service.DisconnectPlayer(_room, guest);

        Assert.Empty(reports);
        Assert.Null(guest.AccountId);
        Assert.Null(service.FinishedStatsOf(guest));
    }

    [Fact]
    public async Task CheckFoodCollision_AnAccountPlayer_CountsWhatItAte_NotTheCredit()
    {
        var (service, reports) = Reporting();
        var player = AddAccountPlayer(service, AccountA, "vini", 500, 500);
        AddFood(500, 500, 3, credit: 2);
        await service.CheckFoodCollision(_state, player);
        AddFood(500, 500, 20, FoodKind.Prize, credit: 8);
        await service.CheckFoodCollision(_state, player);

        await service.DisconnectPlayer(_room, player);

        var report = ReportOf(reports, AccountA);
        Assert.Equal((2L, 1L), (report.Counters[StatKeys.Foods], report.Counters[StatKeys.Prizes]));
        Assert.Equal(23, report.Maxima[StatKeys.BestScore]);
    }

    [Fact]
    public async Task CheckObstacleCollision_AHeadShot_CountsAKillForTheShooterAndADeathForTheTarget()
    {
        var (service, reports) = Reporting();
        var target = AddAccountPlayer(service, AccountA, "target", 500, 500, length: 11, score: 5);
        var shooter = AddAccountPlayer(service, AccountB, "shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await service.CheckObstacleCollision(_state, target);
        await service.DisconnectPlayer(_room, target);
        await service.DisconnectPlayer(_room, shooter);

        var victim = ReportOf(reports, AccountA);
        var killer = ReportOf(reports, AccountB);
        Assert.Equal((1L, 0L), (victim.Counters[StatKeys.Deaths], victim.Counters[StatKeys.Kills]));
        Assert.Equal((0L, 1L, 1L), (killer.Counters[StatKeys.Deaths], killer.Counters[StatKeys.Kills], killer.Maxima[StatKeys.BestKillsInLife]));
    }

    [Fact]
    public async Task ResolveSnakeCollisions_TheSnakeRunIntoGetsTheKill()
    {
        var (service, reports) = Reporting();
        var wall = AddAccountPlayer(service, AccountA, "wall", 500, 500);
        var intruder = AddPlayer("intruder", 900, 900, length: 8, score: 2);
        intruder.CurrentPosition = At(wall.Path[2].X, 505);
        intruder.Path[^1] = intruder.CurrentPosition;

        await service.ResolveSnakeCollisions(_state);
        await service.DisconnectPlayer(_room, wall);

        Assert.Equal(1, ReportOf(reports, AccountA).Counters[StatKeys.Kills]);
    }

    [Fact]
    public async Task FinishedStatsOf_ADeadAccountPlayer_HoldsItsLifeUntilItIsRemoved()
    {
        var (service, reports) = Reporting();
        var target = AddAccountPlayer(service, AccountA, "target", 500, 500, length: 11, score: 5);
        var shooter = AddPlayer("shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);
        await service.CheckObstacleCollision(_state, target);

        var finished = service.FinishedStatsOf(target);
        await service.DisconnectPlayer(_room, target);

        Assert.NotNull(finished);
        Assert.Equal(1, finished!.Counters[StatKeys.Deaths]);
        Assert.Same(finished, ReportOf(reports, AccountA));
        Assert.Null(service.FinishedStatsOf(target));
    }

    [Fact]
    public void FinishedStatsOf_AnAccountPlayerStillAlive_IsNull()
    {
        var (service, _) = Reporting();

        var player = AddAccountPlayer(service, AccountA, "vini", 500, 500);

        Assert.Null(service.FinishedStatsOf(player));
    }

    [Fact]
    public async Task ResolveBossHits_TheLastShot_CountsABossKillForTheShooter_AHurtingOneDoesNot()
    {
        var (service, reports) = Reporting();
        var hurter = AddAccountPlayer(service, AccountA, "hurter", 100, 100);
        var finisher = AddAccountPlayer(service, AccountB, "finisher", 1500, 100);
        AddBoss(800, 400, hp: 2);
        AddShot(hurter, 810, 410);
        await service.ResolveBossHits(_state);
        AddShot(finisher, 810, 410);
        await service.ResolveBossHits(_state);

        await service.DisconnectPlayer(_room, hurter);
        await service.DisconnectPlayer(_room, finisher);

        Assert.Null(_state.Boss);
        Assert.Equal(0, ReportOf(reports, AccountA).Counters[StatKeys.BossKills]);
        Assert.Equal(1, ReportOf(reports, AccountB).Counters[StatKeys.BossKills]);
    }

    [Fact]
    public async Task StrikeArea_ABrokenBody_CountsAHitOfTheStrikesKind()
    {
        var (service, reports) = Reporting();
        var victim = AddAccountPlayer(service, AccountA, "victim", 500, 500, length: 11, score: 10);

        service.StrikeArea(_state, Middle(victim.Path[0].X), Middle(500), 5, WorldStrikeKind.Meteor);
        service.StrikeArea(_state, Middle(victim.Path[0].X), Middle(500), 5);
        await service.DisconnectPlayer(_room, victim);

        var report = ReportOf(reports, AccountA);
        Assert.Equal((1L, 1L), (report.Counters[StatKeys.MeteorHits], report.Counters[StatKeys.LightningHits]));
    }

    [Fact]
    public async Task StrikeArea_AHeadHit_CountsAHitAndADeath()
    {
        var (service, reports) = Reporting();
        var victim = AddAccountPlayer(service, AccountA, "victim", 500, 500, length: 11, score: 10);

        service.StrikeArea(_state, Middle(500), Middle(500), 5, WorldStrikeKind.Meteor);
        await service.DisconnectPlayer(_room, victim);

        var report = ReportOf(reports, AccountA);
        Assert.Equal((1L, 1L), (report.Counters[StatKeys.MeteorHits], report.Counters[StatKeys.Deaths]));
    }

    [Fact]
    public async Task StrikeArea_NoMassLost_CountsNoHit()
    {
        var (service, reports) = Reporting();
        var small = AddAccountPlayer(service, AccountA, "small", 500, 500, length: InitialLength, score: 5);

        service.StrikeArea(_state, Middle(small.Path[0].X), Middle(500), 5);
        await service.DisconnectPlayer(_room, small);

        Assert.Equal(0, ReportOf(reports, AccountA).Counters[StatKeys.LightningHits]);
    }

    [Fact]
    public async Task ApplyZoneDamage_CountsForNeitherKindOfStrike()
    {
        var (service, reports) = Reporting();
        var outside = AddAccountPlayer(service, AccountA, "outside", 1500, 900, score: 10);

        service.ApplyZoneDamage(_state, 300, 300, 100, 4);
        await service.DisconnectPlayer(_room, outside);

        var report = ReportOf(reports, AccountA);
        Assert.Equal((0L, 0L), (report.Counters[StatKeys.LightningHits], report.Counters[StatKeys.MeteorHits]));
    }

    [Fact]
    public async Task MovePlayer_KeepsThePeakMassAndLengthOfTheLife()
    {
        var (service, reports) = Reporting();
        var player = AddAccountPlayer(service, AccountA, "vini", 500, 500, score: 4);
        foreach (var _ in Enumerable.Range(0, 10))
            await service.MovePlayer(_state, player);
        var peakLength = player.Path.Count;
        player.Score = 0;
        foreach (var _ in Enumerable.Range(0, 10))
            await service.MovePlayer(_state, player);

        await service.DisconnectPlayer(_room, player);

        var report = ReportOf(reports, AccountA);
        Assert.True(peakLength > InitialLength);
        Assert.Equal(InitialLength, player.Path.Count);
        Assert.Equal((4L, (long)peakLength), (report.Maxima[StatKeys.BestScore], report.Maxima[StatKeys.LongestSnake]));
    }

    [Fact]
    public async Task Service_WithoutAReporter_StillCountsAndRemovesPlayers()
    {
        var player = AddPlayer("vini", 500, 500);
        _service.AttachAccount(player, AccountA);

        var removed = await _service.DisconnectPlayer(_room, player);

        Assert.True(removed);
        Assert.Empty(_state.Players);
    }

    [Fact]
    public async Task Shoot_AnAccountPlayer_CountsTheShotItFired()
    {
        var (service, reports) = Reporting();
        var shooter = AddAccountPlayer(service, AccountA, "shooter", 500, 500, score: 5);

        await service.Shoot(_state, _room, shooter);
        await service.DisconnectPlayer(_room, shooter);

        Assert.Equal(1, ReportOf(reports, AccountA).Counters[StatKeys.ShotsFired]);
    }

    [Fact]
    public async Task CheckObstacleCollision_AShotOnTheBody_CountsAHitAndTheDamageForTheShooter_NotForTheTarget()
    {
        var (service, reports) = Reporting();
        var target = AddAccountPlayer(service, AccountA, "target", 500, 500);
        var shooter = AddAccountPlayer(service, AccountB, "shooter", 1500, 100);
        AddShot(shooter, target.Path[2].X - 0.1, 500);

        await service.CheckObstacleCollision(_state, target);
        await service.DisconnectPlayer(_room, target);
        await service.DisconnectPlayer(_room, shooter);

        var dealt = ReportOf(reports, AccountB).Counters;
        var suffered = ReportOf(reports, AccountA).Counters;
        Assert.Equal(1, dealt[StatKeys.ShotsHit]);
        Assert.True(dealt[StatKeys.DamageDealt] >= 1);
        Assert.Equal(0, dealt[StatKeys.SegmentsBroken]);
        Assert.Equal((0L, 0L), (suffered[StatKeys.ShotsHit], suffered[StatKeys.DamageDealt]));
    }

    [Fact]
    public async Task CheckObstacleCollision_AHeadShot_CountsAHitWithoutDamage()
    {
        var (service, reports) = Reporting();
        var target = AddAccountPlayer(service, AccountA, "target", 500, 500, length: 11, score: 5);
        var shooter = AddAccountPlayer(service, AccountB, "shooter", 1500, 100);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await service.CheckObstacleCollision(_state, target);
        await service.DisconnectPlayer(_room, shooter);

        var dealt = ReportOf(reports, AccountB).Counters;
        Assert.Equal((1L, 0L), (dealt[StatKeys.ShotsHit], dealt[StatKeys.DamageDealt]));
    }

    [Fact]
    public async Task CheckObstacleCollision_AShotThatBreaksASegmentOff_CountsItForTheShooter()
    {
        var (service, reports) = Reporting();
        var target = AddPlayer("target", 500, 500, length: 8, score: 2);
        target.SegmentHealth[2] = 0.05;
        var shooter = AddAccountPlayer(service, AccountB, "shooter", 1500, 100);
        AddShot(shooter, target.Path[2].X - 0.1, 500);

        await service.CheckObstacleCollision(_state, target);
        await service.DisconnectPlayer(_room, shooter);

        Assert.Equal(1, ReportOf(reports, AccountB).Counters[StatKeys.SegmentsBroken]);
    }

    [Fact]
    public async Task MovePlayer_Boosting_AddsTheTimeSpentBoosting_AndNotTheTimeNotBoosting()
    {
        var (service, reports) = Reporting();
        var player = AddAccountPlayer(service, AccountA, "boost", 500, 500, score: 50);
        player.IsBoosting = true;
        foreach (var _ in Enumerable.Range(0, 60)) await service.MovePlayer(_state, player);
        player.IsBoosting = false;
        foreach (var _ in Enumerable.Range(0, 60)) await service.MovePlayer(_state, player);

        await service.DisconnectPlayer(_room, player);

        var seconds = ReportOf(reports, AccountA).Counters[StatKeys.BoostSeconds];
        var expected = (long)(60 * _tickRate.TickRate.TotalSeconds);
        Assert.Equal(expected, seconds);
    }
}
