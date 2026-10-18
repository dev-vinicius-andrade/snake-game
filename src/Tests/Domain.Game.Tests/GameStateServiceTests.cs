using System.Collections.Concurrent;
using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Color = Domain.Game.Entities.Color;

namespace Domain.Game.Tests;

/// <summary>Wiring of the pure rules inside <see cref="GameStateService"/>: real rules, real entities, faked collaborators.</summary>
public sealed class GameStateServiceTests
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

    private GameStateService Build(PerkRules perks)
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
            perks);
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

    private void AddFood(double x, double y, long value, FoodKind kind = FoodKind.Normal, int credit = 1)
    {
        var food = new Food(Guid.NewGuid())
        {
            Position = At(x, y), Color = new Color("#00ff00", "#00ff00"), Value = value, Kind = kind, FoodsCredit = credit
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
}
