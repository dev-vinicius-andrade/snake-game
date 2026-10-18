using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;
using Color = Domain.Game.Entities.Color;

namespace Domain.Game.Tests;

public sealed class PrizeSpawnerTests
{
    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly FakeTime _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly PrizeOptions _options = new() { SpawnIntervalSeconds = 60, LifetimeSeconds = 30, Value = 25, FoodsCredit = 6, MinPlayers = 2 };
    private readonly PrizeSpawner _spawner;
    private readonly GameState _state;

    public PrizeSpawnerTests()
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        room.Width = 1920;
        room.Height = 1080;
        _state = new GameState(room);
        _spawner = new PrizeSpawner(new PrizeRules(_options), _options, _time);
    }

    private void AddPlayers(int count)
    {
        for (var index = 0; index < count; index++)
        {
            var player = new Player(Guid.NewGuid(), $"p{index}");
            _state.Players.Add(player.Guid, player);
        }
    }

    private IFood[] Prizes() => _state.Foods.Values.Where(food => food.Kind == FoodKind.Prize).ToArray();

    private void Pass(int seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        _spawner.Update(_state);
    }

    [Fact]
    public void Update_BeforeTheFirstInterval_PutsNothingOnTheMap()
    {
        AddPlayers(2);
        _spawner.Update(_state);

        Pass(59);

        Assert.Empty(Prizes());
    }

    [Fact]
    public void Update_AfterTheInterval_PutsOnePrizeOnTheMap()
    {
        AddPlayers(2);
        _spawner.Update(_state);

        Pass(60);

        var prize = Assert.Single(Prizes());
        Assert.Equal(25, prize.Value);
        Assert.Equal(6, prize.FoodsCredit);
        Assert.InRange(prize.Position.X, 0, 1920);
        Assert.InRange(prize.Position.Y, 0, 1080);
    }

    [Fact]
    public void Update_WhilePrizeIsOnTheMap_DoesNotAddAnother()
    {
        AddPlayers(2);
        _spawner.Update(_state);
        Pass(60);

        Pass(10);
        Pass(10);

        Assert.Single(Prizes());
    }

    [Fact]
    public void Update_PrizeNobodyTakes_LeavesTheMapAndComesBackLater()
    {
        AddPlayers(2);
        _spawner.Update(_state);
        Pass(60);

        Pass(30);
        Assert.Empty(Prizes());

        Pass(60);
        Assert.Single(Prizes());
    }

    [Fact]
    public void Update_PrizeEaten_NextOneComesAfterTheInterval()
    {
        AddPlayers(2);
        _spawner.Update(_state);
        Pass(60);
        _state.Foods.Remove(Prizes()[0].Guid);

        Pass(1);
        Pass(59);
        Assert.Empty(Prizes());

        Pass(1);
        Assert.Single(Prizes());
    }

    [Fact]
    public void Update_AloneInTheRoom_WaitsForCompany()
    {
        AddPlayers(1);
        _spawner.Update(_state);

        Pass(600);
        Assert.Empty(Prizes());

        AddPlayers(1);
        Pass(1);
        Assert.Single(Prizes());
    }

    [Fact]
    public void Update_RoomEmptied_TakesThePrizeOffTheMapAndStartsOver()
    {
        AddPlayers(2);
        _spawner.Update(_state);
        Pass(60);
        _state.Players.Clear();

        Pass(1);

        Assert.Empty(Prizes());
    }

    [Fact]
    public void Update_LeavesOrdinaryFoodAlone()
    {
        AddPlayers(2);
        var food = new Food(Guid.NewGuid()) { Position = new PointAxis(), Color = new Color("#fff", "#fff") };
        _state.Foods.Add(food.Guid, food);
        _spawner.Update(_state);

        Pass(60);
        Pass(30);

        Assert.Contains(food.Guid, _state.Foods.Keys);
    }
}
