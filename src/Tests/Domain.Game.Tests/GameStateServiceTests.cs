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
    private readonly DamageCalculator _damage = new(new DamageModelOptions());
    private readonly GameStateService _service;
    private readonly IGameState _state;
    private readonly IRoom _room = Substitute.For<IRoom>();

    public GameStateServiceTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1920;
        _room.Height = 1080;
        _state = new GameState(_room);
        var states = new ConcurrentDictionary<Guid, IGameState> { [_room.Guid] = _state };
        var tickRate = Substitute.For<ITickRateService>();
        tickRate.TickRate.Returns(TimeSpan.FromMilliseconds(1000 / 60.0));
        var rules = new SnakeRulesOptions();
        _obstacleService.Create(default!, default!, default!, default!, default, default!).ReturnsForAnyArgs(call =>
            new Obstacle(Guid.NewGuid(), HeadSize, _room, call.ArgAt<ITrackableId>(5))
            {
                InitialPosition = call.ArgAt<IPointAxis>(2),
                CurrentPosition = call.ArgAt<IPointAxis>(2),
                EndPosition = _axes.Create(5000, 5000),
                Direction = call.ArgAt<IMovementDirection>(3)
            });
        _service = new GameStateService(
            Options.Create(new ServerConfiguration()),
            Options.Create(new SnakeConfiguration { Speed = 15, HeadSize = HeadSize, InitialSnakeSize = InitialLength - 1 }),
            _playerService,
            Substitute.For<IPlayerRoomService>(),
            Substitute.For<IRoomsService>(),
            states,
            new TrackableIdGeneratorService(),
            Substitute.For<IKnowDirectionsService>(),
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
            new ShotCooldownRules(rules));
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

    private void AddFood(double x, double y, long value)
    {
        var food = new Food(Guid.NewGuid()) { Position = At(x, y), Color = new Color("#00ff00", "#00ff00"), Value = value };
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
}
