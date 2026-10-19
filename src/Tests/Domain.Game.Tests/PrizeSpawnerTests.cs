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
    private readonly PrizeRules _rules;
    private readonly PrizeSpawner _spawner;
    private readonly GameState _state;

    public PrizeSpawnerTests()
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        room.Width = 1920;
        room.Height = 1080;
        _state = new GameState(room);
        _rules = new PrizeRules(_options);
        _spawner = new PrizeSpawner(_rules, _time);
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

    [Fact]
    public void Update_UsesTheValueItWasReconfiguredWith()
    {
        AddPlayers(2);
        _spawner.Update(_state);
        _rules.Reconfigure(new PrizeOptions { SpawnIntervalSeconds = 60, LifetimeSeconds = 30, Value = 99, FoodsCredit = 2, MinPlayers = 2 });

        Pass(60);

        var prize = Assert.Single(Prizes());
        Assert.Equal(99, prize.Value);
        Assert.Equal(2, prize.FoodsCredit);
    }

    private static PrizeDefinition Kind(string id, int interval = 60, long value = 20, string? perk = null, string color = "#ffd23f", params string[] rooms)
        => new() { Id = id, Name = id, SpawnIntervalSeconds = interval, LifetimeSeconds = 30, Value = value, MinPlayers = 1, GrantsPerk = perk, Color = color, Rooms = rooms.ToList() };

    private PrizeSpawner SpawnerWith(params PrizeDefinition[] kinds)
    {
        var rules = new PrizeRules(new PrizeOptions { Prizes = kinds.ToList() });
        return new PrizeSpawner(rules, _time);
    }

    [Fact]
    public void Update_SeveralKindsAppearOnTheirOwnTimes()
    {
        AddPlayers(1);
        var spawner = SpawnerWith(Kind("fast", interval: 10), Kind("slow", interval: 100));
        spawner.Update(_state);

        _time.Now = _time.Now.AddSeconds(10);
        spawner.Update(_state);
        Assert.Equal(new[] { "fast" }, Prizes().Select(food => food.PrizeId).ToArray());

        // The fast one ran out after its 30 seconds; the slow one is due now.
        _time.Now = _time.Now.AddSeconds(90);
        spawner.Update(_state);
        Assert.Equal(new[] { "slow" }, Prizes().Select(food => food.PrizeId).ToArray());
    }

    [Fact]
    public void Update_AKindThatGrantsAPerkCarriesItAndItsColour()
    {
        AddPlayers(1);
        var spawner = SpawnerWith(Kind("gift", perk: "magnet", color: "#ff00aa"));
        spawner.Update(_state);

        _time.Now = _time.Now.AddSeconds(60);
        spawner.Update(_state);

        var prize = Assert.Single(Prizes());
        Assert.Equal("magnet", prize.GrantsPerk);
        Assert.Equal("#ff00aa", prize.Color.FillColor);
        Assert.Equal("gift", prize.PrizeId);
    }

    [Fact]
    public void Update_OnlyOnePrizeOfAKindAtATime_ButDifferentKindsTogether()
    {
        AddPlayers(1);
        var spawner = SpawnerWith(Kind("a", interval: 10), Kind("b", interval: 10));
        spawner.Update(_state);

        _time.Now = _time.Now.AddSeconds(10);
        spawner.Update(_state);
        _time.Now = _time.Now.AddSeconds(5);
        spawner.Update(_state);

        Assert.Equal(2, Prizes().Length);
    }

    [Fact]
    public void Update_AKindForCertainRoomsOnlyAppearsThere()
    {
        AddPlayers(1);
        var spawner = SpawnerWith(Kind("here", interval: 10, rooms: _state.Room.Guid.ToString()), Kind("there", interval: 10, rooms: Guid.NewGuid().ToString()));
        spawner.Update(_state);

        _time.Now = _time.Now.AddSeconds(10);
        spawner.Update(_state);

        Assert.Equal(new[] { "here" }, Prizes().Select(food => food.PrizeId).ToArray());
    }

    [Fact]
    public void Update_AKindThatIsSwitchedOffOrRemovedTakesItsPrizeOffTheMap()
    {
        AddPlayers(1);
        var rules = new PrizeRules(new PrizeOptions { Prizes = [Kind("a", interval: 10), Kind("b", interval: 10)] });
        var spawner = new PrizeSpawner(rules, _time);
        spawner.Update(_state);
        _time.Now = _time.Now.AddSeconds(10);
        spawner.Update(_state);
        Assert.Equal(2, Prizes().Length);

        rules.Reconfigure(new PrizeOptions { Prizes = [Kind("a", interval: 10)] });
        spawner.Update(_state);

        Assert.Equal(new[] { "a" }, Prizes().Select(food => food.PrizeId).ToArray());
    }

    [Fact]
    public void Update_AllPrizesOffTakesThemAllOffTheMap()
    {
        AddPlayers(1);
        var rules = new PrizeRules(new PrizeOptions { Prizes = [Kind("a", interval: 10)] });
        var spawner = new PrizeSpawner(rules, _time);
        spawner.Update(_state);
        _time.Now = _time.Now.AddSeconds(10);
        spawner.Update(_state);

        rules.Reconfigure(new PrizeOptions { Enabled = false, Prizes = [Kind("a", interval: 10)] });
        spawner.Update(_state);

        Assert.Empty(Prizes());
    }
}
