using System.Text.Json;
using Application.Game.Server.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class SnapshotBuilderTests
{
    private static readonly JsonSerializerOptions Options = GameJsonOptions.Create();
    private readonly IGameState _state = Substitute.For<IGameState>();
    private readonly Dictionary<Guid, IPlayer> _players = new();
    private readonly Dictionary<Guid, IFood> _foods = new();
    private readonly Dictionary<Guid, IObstacle> _obstacles = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, WorldStrike> _strikes = new();

    public SnapshotBuilderTests()
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        room.Width.Returns(1920);
        room.Height.Returns(1080);
        _state.Room.Returns(room);
        _state.Players.Returns(_players);
        _state.Foods.Returns(_foods);
        _state.Obstacles.Returns(_obstacles);
        _state.Strikes.Returns(_strikes);
    }

    private static IPointAxis Point(double x, double y)
    {
        var point = Substitute.For<IPointAxis>();
        point.X.Returns(x);
        point.Y.Returns(y);
        return point;
    }

    private static IColor Colour(string fill)
    {
        var color = Substitute.For<IColor>();
        color.FillColor.Returns(fill);
        return color;
    }

    private IPlayer AddPlayer(string name, params (double X, double Y)[] path)
    {
        var player = Substitute.For<IPlayer>();
        player.Guid.Returns(Guid.NewGuid());
        player.Id.Returns("conn-" + name);
        player.Name.Returns(name);
        player.IsAlive.Returns(true);
        var colour = Colour("#aa5500");
        player.BackgroundColor.Returns(colour);
        player.CurrentDirection.Returns(Direction.Right);
        player.Perks.Returns(new List<string>());
        var points = path.Select(p => Point(p.X, p.Y)).ToList();
        var health = path.Select(_ => 1.0).ToList();
        player.Path.Returns(points);
        player.SegmentHealth.Returns(health);
        _players[player.Guid] = player;
        return player;
    }

    private IFood AddFood(double x, double y, long value = 1, FoodKind kind = FoodKind.Normal, string color = "#12ab34")
    {
        var food = Substitute.For<IFood>();
        food.Guid.Returns(Guid.NewGuid());
        var position = Point(x, y);
        var fill = Colour(color);
        food.Position.Returns(position);
        food.Color.Returns(fill);
        food.Value.Returns(value);
        food.Kind.Returns(kind);
        _foods[food.Guid] = food;
        return food;
    }

    private JsonElement Json() => JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state), Options)).RootElement;

    [Fact]
    public void Build_HasTheRoom()
    {
        var room = Json().GetProperty("room");

        Assert.Equal("11111111-1111-1111-1111-111111111111", room.GetProperty("guid").GetString());
        Assert.Equal(1920, room.GetProperty("width").GetInt32());
        Assert.Equal(1080, room.GetProperty("height").GetInt32());
    }

    [Fact]
    public void Build_SnakePathIsOneFlatListOfCoordinatesRoundedToATenth()
    {
        AddPlayer("ana", (100.04, 200.06), (115.449, 200.0), (130.5, 200.0));

        var snake = Json().GetProperty("snakes")[0];

        Assert.Equal(new[] { 100.0, 200.1, 115.4, 200.0, 130.5, 200.0 }, snake.GetProperty("path").EnumerateArray().Select(n => n.GetDouble()));
    }

    [Fact]
    public void Build_SnakeHasWhatTheClientDraws()
    {
        var player = AddPlayer("ana", (1, 2), (3, 4));
        player.Score.Returns(7);
        player.FoodsEaten.Returns(9);
        player.IsBoosting.Returns(true);
        player.AutoFire.Returns(true);
        player.Perks.Returns(new List<string> { "swift" });

        var snake = Json().GetProperty("snakes")[0];

        Assert.Equal(player.Guid.ToString(), snake.GetProperty("guid").GetString());
        Assert.Equal("conn-ana", snake.GetProperty("id").GetString());
        Assert.Equal("ana", snake.GetProperty("name").GetString());
        Assert.Equal(7, snake.GetProperty("score").GetInt64());
        Assert.Equal(9, snake.GetProperty("foodsEaten").GetInt32());
        Assert.True(snake.GetProperty("boost").GetBoolean());
        Assert.True(snake.GetProperty("autoFire").GetBoolean());
        Assert.Equal("#aa5500", snake.GetProperty("color").GetString());
        Assert.Equal("right", snake.GetProperty("direction").GetString());
        Assert.Equal("swift", snake.GetProperty("perks")[0].GetString());
    }

    [Fact]
    public void Build_PerkTiersComeAlongsideThePerks_WhenAnyIsAboveTheFirst()
    {
        var player = AddPlayer("ana", (1, 2), (3, 4));
        player.Perks.Returns(new List<string> { "magnet", "swift" });
        player.PerkTiers.Returns(new List<int> { 2, 1 });

        var snake = Json().GetProperty("snakes")[0];

        Assert.Equal(new[] { 2, 1 }, snake.GetProperty("pt").EnumerateArray().Select(n => n.GetInt32()));
    }

    [Fact]
    public void Build_PerkTiersAreLeftOutWhileEveryPerkIsAtTheFirst()
    {
        var player = AddPlayer("ana", (1, 2), (3, 4));
        player.Perks.Returns(new List<string> { "magnet" });
        player.PerkTiers.Returns(new List<int> { 1 });

        Assert.False(Json().GetProperty("snakes")[0].TryGetProperty("pt", out _));
    }

    [Fact]
    public void Build_LeavesOutWhatIsPlainSoItCostsNothing()
    {
        AddPlayer("ana", (1, 2), (3, 4));

        var snake = Json().GetProperty("snakes")[0];

        foreach (var name in new[] { "dead", "boost", "autoFire", "perks", "health", "foodsEaten" })
            Assert.False(snake.TryGetProperty(name, out _), name);
    }

    [Fact]
    public void Build_HealthIsWholePercentsAndOnlySentWhenSomethingIsHurt()
    {
        var player = AddPlayer("ana", (1, 2), (3, 4), (5, 6));
        player.SegmentHealth.Returns(new List<double> { 0.504, 1.0, 0.0 });

        var snake = Json().GetProperty("snakes")[0];

        Assert.Equal(new[] { 50, 100, 0 }, snake.GetProperty("health").EnumerateArray().Select(n => n.GetInt32()));
    }

    [Fact]
    public void Build_ASnakeThatDiedSaysSo()
    {
        AddPlayer("ana", (1, 2)).IsAlive.Returns(false);

        Assert.True(Json().GetProperty("snakes")[0].GetProperty("dead").GetBoolean());
    }

    [Fact]
    public void Build_SnakeWithoutAPathIsLeftOut()
    {
        AddPlayer("ana");

        Assert.Equal(0, Json().GetProperty("snakes").GetArrayLength());
    }

    [Fact]
    public void Build_FoodIsPositionColourAndOnlyWhatIsUnusual()
    {
        AddFood(10, 20);
        AddFood(30, 40, value: 20, kind: FoodKind.Prize, color: "#ffd23f");

        var foods = Json().GetProperty("foods").EnumerateArray().ToArray();

        var plain = foods.Single(food => food.GetProperty("x").GetInt32() == 10);
        Assert.Equal(20, plain.GetProperty("y").GetInt32());
        Assert.Equal("#12ab34", plain.GetProperty("c").GetString());
        Assert.False(plain.TryGetProperty("v", out _));
        Assert.False(plain.TryGetProperty("k", out _));
        var prize = foods.Single(food => food.GetProperty("x").GetInt32() == 30);
        Assert.Equal(20, prize.GetProperty("v").GetInt64());
        Assert.Equal(1, prize.GetProperty("k").GetInt32());
    }

    [Fact]
    public void Build_NoZone_LeavesItOut()
    {
        Assert.False(Json().TryGetProperty("zone", out _));
    }

    [Fact]
    public void Build_AZone_IsWhereItIsHowFarItReachesNowAndAtTheEndAndWhatPhase()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _state.Zone.Returns(new WorldZone(300.4, 400.6, 812.6, 160, WorldZonePhase.Closing, now.AddMilliseconds(4321)));

        var json = JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state, now), Options)).RootElement;

        var zone = json.GetProperty("zone");
        Assert.Equal(300, zone.GetProperty("x").GetInt32());
        Assert.Equal(401, zone.GetProperty("y").GetInt32());
        Assert.Equal(813, zone.GetProperty("r").GetInt32());
        Assert.Equal(160, zone.GetProperty("f").GetInt32());
        Assert.Equal(1, zone.GetProperty("p").GetInt32());
        Assert.Equal(4321, zone.GetProperty("t").GetInt32());
    }

    [Fact]
    public void Build_AZonePastItsPhase_NeverSaysNegativeTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _state.Zone.Returns(new WorldZone(1, 2, 100, 100, WorldZonePhase.Holding, now.AddMilliseconds(-50)));

        var json = JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state, now), Options)).RootElement;

        Assert.Equal(0, json.GetProperty("zone").GetProperty("t").GetInt32());
    }

    [Fact]
    public void Build_NoBoss_LeavesItOut()
    {
        Assert.False(Json().TryGetProperty("boss", out _));
    }

    [Fact]
    public void Build_ABoss_IsWhereItIsHowBigHowHurtAndWhichWayItPoints()
    {
        var boss = new WorldBoss { Id = Guid.NewGuid(), X = 800.4, Y = 400.6, Size = 60, Hp = 15, MaxHp = 40, Angle = Math.PI / 2, Arms = 6 };
        _state.Boss.Returns(boss);

        var sent = Json().GetProperty("boss");

        Assert.Equal(boss.Id.ToString(), sent.GetProperty("guid").GetString());
        Assert.Equal(800, sent.GetProperty("x").GetInt32());
        Assert.Equal(401, sent.GetProperty("y").GetInt32());
        Assert.Equal(60, sent.GetProperty("s").GetInt32());
        Assert.Equal(38, sent.GetProperty("hp").GetInt32());
        Assert.Equal(90, sent.GetProperty("a").GetInt32());
        Assert.Equal(6, sent.GetProperty("n").GetInt32());
    }

    [Fact]
    public void Build_ABossAlmostDead_StillSaysItIsThere()
    {
        _state.Boss.Returns(new WorldBoss { Id = Guid.NewGuid(), X = 1, Y = 1, Size = 60, Hp = 0.2, MaxHp = 400, Arms = 1 });

        Assert.Equal(1, Json().GetProperty("boss").GetProperty("hp").GetInt32());
    }

    [Fact]
    public void Build_NoLightningWarning_LeavesTheListOut()
    {
        Assert.False(Json().TryGetProperty("strikes", out _));
    }

    [Fact]
    public void Build_LightningWarning_IsSpotRadiusAndMillisecondsLeft()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _strikes[Guid.NewGuid()] = new WorldStrike(100.4, 200.6, 90, now.AddMilliseconds(1234));

        var json = JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state, now), Options)).RootElement;

        var strike = json.GetProperty("strikes").EnumerateArray().Single();
        Assert.Equal(100, strike.GetProperty("x").GetInt32());
        Assert.Equal(201, strike.GetProperty("y").GetInt32());
        Assert.Equal(90, strike.GetProperty("r").GetInt32());
        Assert.Equal(1234, strike.GetProperty("t").GetInt32());
        Assert.Equal(0, strike.GetProperty("k").GetInt32());
    }

    [Fact]
    public void Build_AMeteorSaysItIsOne_SoTheClientDrawsItAsOne()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _strikes[Guid.NewGuid()] = new WorldStrike(10, 20, 130, now.AddMilliseconds(2000), WorldStrikeKind.Meteor);
        _strikes[Guid.NewGuid()] = new WorldStrike(30, 40, 90, now.AddMilliseconds(500), WorldStrikeKind.Lightning);

        var json = JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state, now), Options)).RootElement;

        var kinds = json.GetProperty("strikes").EnumerateArray().ToDictionary(strike => strike.GetProperty("x").GetInt32(), strike => strike.GetProperty("k").GetInt32());
        Assert.Equal(1, kinds[10]);
        Assert.Equal(0, kinds[30]);
    }

    [Fact]
    public void Build_LightningWarningPastItsMoment_NeverSaysNegativeTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _strikes[Guid.NewGuid()] = new WorldStrike(1, 2, 90, now.AddMilliseconds(-50));

        var json = JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state, now), Options)).RootElement;

        Assert.Equal(0, json.GetProperty("strikes")[0].GetProperty("t").GetInt32());
    }

    [Fact]
    public void Build_ShotHasPositionSizeAndWhoShotIt()
    {
        var shooter = AddPlayer("ana", (1, 2));
        var shot = Substitute.For<IObstacle>();
        shot.Guid.Returns(Guid.NewGuid());
        var at = Point(30.04, 40.0);
        shot.CurrentPosition.Returns(at);
        shot.Size.Returns(15);
        var owner = Substitute.For<ITrackableId>();
        var shooterGuid = shooter.Guid;
        owner.Guid.Returns(shooterGuid);
        shot.CreatedyBy.Returns(owner);
        _obstacles[shot.Guid] = shot;

        var sent = Json().GetProperty("shots")[0];

        Assert.Equal(shot.Guid.ToString(), sent.GetProperty("guid").GetString());
        Assert.Equal(30.0, sent.GetProperty("x").GetDouble());
        Assert.Equal(40.0, sent.GetProperty("y").GetDouble());
        Assert.Equal(15, sent.GetProperty("size").GetInt64());
        Assert.Equal(shooter.Guid.ToString(), sent.GetProperty("owner").GetString());
    }

    [Fact]
    public void Build_IsMuchSmallerThanSendingTheEntities()
    {
        for (var snake = 0; snake < 5; snake++)
            AddPlayer("p" + snake, Enumerable.Range(0, 60).Select(i => (100.0 + i * 15.123456, 100.0 + snake * 80.654321)).ToArray());
        for (var food = 0; food < 50; food++) AddFood(food * 30, food * 20);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(SnapshotBuilder.Build(_state), Options).Length;

        Assert.True(bytes < 9_000, $"{bytes} bytes");
    }

    private JsonElement JsonAt(DateTimeOffset now) => JsonDocument.Parse(JsonSerializer.Serialize(SnapshotBuilder.Build(_state, now), Options)).RootElement;

    [Fact]
    public void Build_NothingGoingOn_LeavesOutMinesBountyShowerAndRush()
    {
        var json = Json();

        Assert.False(json.TryGetProperty("mines", out _));
        Assert.False(json.TryGetProperty("bounty", out _));
        Assert.False(json.TryGetProperty("shower", out _));
        Assert.False(json.TryGetProperty("rush", out _));
    }

    [Fact]
    public void Build_Mines_AreTheirSpotTheBlastAndTheMillisecondsToArming()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var mines = new System.Collections.Concurrent.ConcurrentDictionary<Guid, WorldMine>();
        var fresh = new WorldMine(Guid.NewGuid(), 100.4, 200.6, 60, 20, now.AddMilliseconds(1500));
        var armed = new WorldMine(Guid.NewGuid(), 300, 400, 60, 20, now.AddMilliseconds(-10));
        mines[fresh.Id] = fresh;
        mines[armed.Id] = armed;
        _state.Mines.Returns(mines);

        var list = JsonAt(now).GetProperty("mines").EnumerateArray().ToList();

        Assert.Equal(2, list.Count);
        var unarmed = list.Single(mine => mine.GetProperty("x").GetInt32() == 100);
        Assert.Equal((201, 60, 1500), (unarmed.GetProperty("y").GetInt32(), unarmed.GetProperty("r").GetInt32(), unarmed.GetProperty("t").GetInt32()));
        Assert.Equal(0, list.Single(mine => mine.GetProperty("x").GetInt32() == 300).GetProperty("t").GetInt32());
    }

    [Fact]
    public void Build_NoMinesLeftLeavesTheListOut()
    {
        _state.Mines.Returns(new System.Collections.Concurrent.ConcurrentDictionary<Guid, WorldMine>());

        Assert.False(Json().TryGetProperty("mines", out _));
    }

    [Fact]
    public void Build_ABounty_IsTheWantedSnakeTheTimeLeftAndTheReward()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var target = Guid.NewGuid();
        _state.Bounty.Returns(new WorldBounty(target, now.AddMilliseconds(61234), 250, 10));

        var bounty = JsonAt(now).GetProperty("bounty");

        Assert.Equal(target, bounty.GetProperty("guid").GetGuid());
        Assert.Equal(61234, bounty.GetProperty("t").GetInt32());
        Assert.Equal(250, bounty.GetProperty("m").GetInt64());
    }

    [Fact]
    public void Build_ABountyPastItsTime_NeverSaysNegativeTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _state.Bounty.Returns(new WorldBounty(Guid.NewGuid(), now.AddMilliseconds(-5), 1, 0));

        Assert.Equal(0, JsonAt(now).GetProperty("bounty").GetProperty("t").GetInt32());
    }

    [Fact]
    public void Build_AFruitShowerAndASugarRush_AreTheMillisecondsLeft()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _state.FruitShowerEndsAt.Returns(now.AddMilliseconds(12000));
        _state.SugarRushEndsAt.Returns(now.AddMilliseconds(29500));

        var json = JsonAt(now);

        Assert.Equal(12000, json.GetProperty("shower").GetInt32());
        Assert.Equal(29500, json.GetProperty("rush").GetInt32());
    }

    [Fact]
    public void Build_NoMaze_LeavesItOut() => Assert.False(Json().TryGetProperty("maze", out _));

    [Fact]
    public void Build_AMaze_IsOnlyWhatTheClientNeedsToBuildTheSameWalls()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var walls = Substitute.For<IMazeWalls>();
        _state.Maze.Returns(new WorldMaze(300.4, 200.6, 12, 8, 4, 60, 4000000000, 15, now.AddMilliseconds(90500), walls, Guid.NewGuid(), 2));

        var maze = JsonAt(now).GetProperty("maze");

        Assert.Equal(300, maze.GetProperty("x").GetInt32());
        Assert.Equal(201, maze.GetProperty("y").GetInt32());
        Assert.Equal(12, maze.GetProperty("c").GetInt32());
        Assert.Equal(8, maze.GetProperty("r").GetInt32());
        Assert.Equal(4, maze.GetProperty("k").GetInt32());
        Assert.Equal(60, maze.GetProperty("v").GetInt32());
        Assert.Equal(4000000000u, maze.GetProperty("s").GetUInt32());
        Assert.Equal(15, maze.GetProperty("u").GetInt32());
        Assert.Equal(90500, maze.GetProperty("t").GetInt32());
        Assert.Equal(9, maze.EnumerateObject().Count());
    }

    [Fact]
    public void Build_AMazePastItsTime_NeverSaysNegativeTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _state.Maze.Returns(new WorldMaze(0, 0, 3, 3, 3, 0, 1, 15, now.AddMilliseconds(-9), Substitute.For<IMazeWalls>(), Guid.NewGuid(), 0));

        Assert.Equal(0, JsonAt(now).GetProperty("maze").GetProperty("t").GetInt32());
    }

    [Fact]
    public void Build_AShowerPastItsTime_NeverSaysNegativeTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _state.FruitShowerEndsAt.Returns(now.AddMilliseconds(-9));

        Assert.Equal(0, JsonAt(now).GetProperty("shower").GetInt32());
    }
}
