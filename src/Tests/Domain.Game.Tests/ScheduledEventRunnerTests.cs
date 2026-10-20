using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class ScheduledEventRunnerTests
{
    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly FakeTime _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IGameStateService _gameStates = Substitute.For<IGameStateService>();
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly GameState _state;
    private int _foods;

    public ScheduledEventRunnerTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1000;
        _room.Height = 500;
        _gameStates.GenerateFood(Arg.Any<IRoom>()).Returns(_ =>
        {
            _foods++;
            return Substitute.For<IFood>();
        });
        _state = new GameState(_room);
        AddPlayer("ana");
        AddPlayer("bo");
        AddPlayer("cy");
    }

    private static WorldEventOptions Quiet(Func<WorldEventOptions, WorldEventOptions>? change = null)
    {
        var none = new WorldEventOptions
        {
            Lightning = new LightningOptions { Enabled = false },
            Meteors = new MeteorOptions { Enabled = false },
            Turret = new TurretOptions { Enabled = false },
            Zone = new ZoneOptions { Enabled = false },
            FruitShower = new FruitShowerOptions { Enabled = false },
            Bounty = new BountyOptions { Enabled = false },
            Mines = new MineFieldOptions { Enabled = false },
            SugarRush = new SugarRushOptions { Enabled = false },
        };
        return change?.Invoke(none) ?? none;
    }

    private WorldEventRules Rules(WorldEventOptions options) => new(options, () => 0.5);

    private Player AddPlayer(string name)
    {
        var player = new Player(Guid.NewGuid(), name) { CurrentPosition = new PointAxis { X = 10, Y = 10 } };
        _state.Players.Add(player.Guid, player);
        return player;
    }

    private void Pass(Action<IGameState> update, double seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        update(_state);
    }

    // Fruit shower

    private static readonly FruitShowerOptions Shower = new() { MinIntervalSeconds = 30, MaxIntervalSeconds = 30, DurationSeconds = 20, FruitsPerSecond = 10, MinPlayers = 1 };

    private FruitShowerRunner Showers(FruitShowerOptions? options = null)
        => new(Rules(Quiet(o => o with { FruitShower = options ?? Shower })), new EventSchedule(() => 0.5), _gameStates, _time);

    [Fact]
    public void FruitShower_BeforeItsTimeNothingFalls()
    {
        var runner = Showers();
        runner.Update(_state);
        Pass(runner.Update, 29);

        Assert.Equal(0, _foods);
        Assert.Null(_state.FruitShowerEndsAt);
    }

    [Fact]
    public void FruitShower_WhenDue_BeginsAndDropsFruitAtItsRate()
    {
        var runner = Showers();
        runner.Update(_state);
        Pass(runner.Update, 30);
        Pass(runner.Update, 1);

        Assert.NotNull(_state.FruitShowerEndsAt);
        Assert.Equal(10, _foods);
    }

    [Fact]
    public void FruitShower_KeepsTheRateWhateverTheFrameLength()
    {
        var runner = Showers();
        runner.Update(_state);
        Pass(runner.Update, 30);
        foreach (var _ in Enumerable.Range(0, 20)) Pass(runner.Update, 0.05);

        Assert.Equal(10, _foods);
    }

    [Fact]
    public void FruitShower_FallsForItsDurationAndThenStops()
    {
        var runner = Showers();
        runner.Update(_state);
        Pass(runner.Update, 30);
        foreach (var _ in Enumerable.Range(0, 20)) Pass(runner.Update, 1);
        Pass(runner.Update, 1);
        var total = _foods;
        Pass(runner.Update, 5);

        Assert.Null(_state.FruitShowerEndsAt);
        Assert.Equal(total, _foods);
        Assert.InRange(total, 190, 210);
    }

    [Fact]
    public void FruitShower_APauseDoesNotPourAllAtOnce()
    {
        var runner = Showers();
        runner.Update(_state);
        Pass(runner.Update, 30);
        Pass(runner.Update, 10);

        Assert.InRange(_foods, 0, 15);
    }

    [Fact]
    public void FruitShower_TheNextOneIsScheduledAfterwards()
    {
        var runner = Showers();
        runner.Update(_state);
        Pass(runner.Update, 30);
        Pass(runner.Update, 21);
        Assert.Null(_state.FruitShowerEndsAt);
        Pass(runner.Update, 30);

        Assert.NotNull(_state.FruitShowerEndsAt);
    }

    [Fact]
    public void FruitShower_Trigger_StartsOneRightAway()
    {
        var runner = Showers();

        Assert.True(runner.Trigger(_state));
        runner.Update(_state);

        Assert.NotNull(_state.FruitShowerEndsAt);
    }

    [Fact]
    public void FruitShower_Trigger_NotWhenOneIsGoingOffOrTheRoomIsEmpty()
    {
        var runner = Showers();
        runner.Trigger(_state);
        runner.Update(_state);
        Assert.False(runner.Trigger(_state));

        Assert.False(Showers(Shower with { Enabled = false }).Trigger(_state));
        var empty = new GameState(_room);
        Assert.False(Showers().Trigger(empty));
    }

    [Fact]
    public void FruitShower_ARoomThatLosesItsPlayersForgetsIt()
    {
        var runner = Showers();
        runner.Trigger(_state);
        runner.Update(_state);
        _state.Players.Clear();

        runner.Update(_state);

        Assert.Null(_state.FruitShowerEndsAt);
    }

    [Fact]
    public void FruitShower_ARoomItIsNotForNeverHasOne()
    {
        var runner = Showers(Shower with { ExcludedRooms = [_room.Guid.ToString()] });

        Assert.False(runner.Trigger(_state));
        runner.Update(_state);
        Pass(runner.Update, 60);

        Assert.Null(_state.FruitShowerEndsAt);
    }

    [Fact]
    public void FruitShower_WaitsForEnoughPlayers()
    {
        var runner = Showers(Shower with { MinPlayers = 4 });
        runner.Update(_state);
        Pass(runner.Update, 60);

        Assert.Null(_state.FruitShowerEndsAt);
    }

    // Sugar rush

    private static readonly SugarRushOptions Rush = new() { MinIntervalSeconds = 30, MaxIntervalSeconds = 30, DurationSeconds = 30 };

    private SugarRushRunner Rushes(SugarRushOptions? options = null)
        => new(Rules(Quiet(o => o with { SugarRush = options ?? Rush })), new EventSchedule(() => 0.5), _time);

    [Fact]
    public void SugarRush_BeginsWhenDueAndEndsAfterItsDuration()
    {
        var runner = Rushes();
        runner.Update(_state);
        Pass(runner.Update, 30);
        Assert.Equal(_time.Now.AddSeconds(30), _state.SugarRushEndsAt);

        Pass(runner.Update, 29);
        Assert.NotNull(_state.SugarRushEndsAt);
        Pass(runner.Update, 1);
        Assert.Null(_state.SugarRushEndsAt);
    }

    [Fact]
    public void SugarRush_Trigger_StartsOneAndNotTwo()
    {
        var runner = Rushes();

        Assert.True(runner.Trigger(_state));
        runner.Update(_state);
        Assert.NotNull(_state.SugarRushEndsAt);
        Assert.False(runner.Trigger(_state));
    }

    [Fact]
    public void SugarRush_SwitchedOffByTheMasterSwitchEndsAtOnce()
    {
        var rules = Rules(Quiet(o => o with { SugarRush = Rush }));
        var runner = new SugarRushRunner(rules, new EventSchedule(() => 0.5), _time);
        runner.Trigger(_state);
        runner.Update(_state);

        rules.Reconfigure(Quiet(o => o with { Enabled = false, SugarRush = Rush }));
        runner.Update(_state);

        Assert.Null(_state.SugarRushEndsAt);
    }

    // Bounty

    private static readonly BountyOptions Wanted = new() { MinIntervalSeconds = 30, MaxIntervalSeconds = 30, DurationSeconds = 90, RewardMass = 200, FoodsCredit = 5, MinPlayers = 3 };

    private BountyRunner Bounties(BountyOptions? options = null)
        => new(Rules(Quiet(o => o with { Bounty = options ?? Wanted })), new EventSchedule(() => 0.5), _time);

    [Fact]
    public void Bounty_MarksALivingSnakeWithTheRewardAndTheTime()
    {
        var runner = Bounties();
        runner.Update(_state);
        Pass(runner.Update, 30);

        Assert.NotNull(_state.Bounty);
        var bounty = _state.Bounty!;
        Assert.Contains(bounty.Target, _state.Players.Keys);
        Assert.Equal((200L, 5, _time.Now.AddSeconds(90)), (bounty.RewardMass, bounty.FoodsCredit, bounty.EndsAt));
    }

    [Fact]
    public void Bounty_NeverMarksADeadSnake()
    {
        var runner = Bounties();
        foreach (var player in _state.Players.Values.Take(2)) player.IsAlive = false;
        var living = _state.Players.Values.Single(player => player.IsAlive);
        runner.Update(_state);
        Pass(runner.Update, 30);

        Assert.Equal(living.Guid, _state.Bounty!.Target);
    }

    [Fact]
    public void Bounty_ItEndsWhenTheWantedSnakeDies()
    {
        var runner = Bounties();
        runner.Update(_state);
        Pass(runner.Update, 30);
        _state.Players[_state.Bounty!.Target].IsAlive = false;

        runner.Update(_state);

        Assert.Null(_state.Bounty);
    }

    [Fact]
    public void Bounty_ItEndsWhenTheWantedSnakeLeaves()
    {
        var runner = Bounties();
        runner.Update(_state);
        Pass(runner.Update, 30);
        _state.Players.Remove(_state.Bounty!.Target);

        runner.Update(_state);

        Assert.Null(_state.Bounty);
    }

    [Fact]
    public void Bounty_ItEndsWhenTheRewardWasPaid_AndTheNextIsScheduledFromThen()
    {
        var runner = Bounties();
        runner.Update(_state);
        Pass(runner.Update, 30);
        _state.Bounty = null;
        Pass(runner.Update, 1);
        Assert.Null(_state.Bounty);

        Pass(runner.Update, 29);
        Assert.Null(_state.Bounty);
        Pass(runner.Update, 1);
        Assert.NotNull(_state.Bounty);
    }

    [Fact]
    public void Bounty_ItEndsWhenTheTimeIsUp()
    {
        var runner = Bounties();
        runner.Update(_state);
        Pass(runner.Update, 30);

        Pass(runner.Update, 90);

        Assert.Null(_state.Bounty);
    }

    [Fact]
    public void Bounty_NeedsEnoughPlayers()
    {
        var runner = Bounties(Wanted with { MinPlayers = 4 });
        runner.Update(_state);
        Pass(runner.Update, 60);

        Assert.Null(_state.Bounty);
    }

    [Fact]
    public void Bounty_TriggerIgnoresTheNumberOfPlayers()
    {
        var runner = Bounties(Wanted with { MinPlayers = 10 });

        Assert.True(runner.Trigger(_state));
        runner.Update(_state);

        Assert.NotNull(_state.Bounty);
    }

    [Fact]
    public void Bounty_ARoomWithoutPlayersForgetsIt()
    {
        var runner = Bounties();
        runner.Trigger(_state);
        runner.Update(_state);
        _state.Players.Clear();

        runner.Update(_state);

        Assert.Null(_state.Bounty);
    }

    // Mine field

    private static readonly MineFieldOptions Field = new()
    {
        MinIntervalSeconds = 30, MaxIntervalSeconds = 30, MinMines = 4, MaxMines = 4, Radius = 50, TriggerRadius = 15, ArmMilliseconds = 2000
    };

    private MineFieldRunner Fields(MineFieldOptions? options = null)
        => new(Rules(Quiet(o => o with { Mines = options ?? Field })), new EventSchedule(() => 0.5), _gameStates, _time);

    [Fact]
    public void Mines_WhenDue_ALayerOfMinesAppearsInsideTheRoomAndIsArmedLater()
    {
        var runner = Fields();
        runner.Update(_state);
        Pass(runner.Update, 30);

        Assert.Equal(4, _state.Mines.Count);
        Assert.All(_state.Mines.Values, mine =>
        {
            Assert.InRange(mine.X, 0, 1000);
            Assert.InRange(mine.Y, 0, 500);
            Assert.Equal((50d, 15d), (mine.Radius, mine.TriggerRadius));
            Assert.Equal(_time.Now.AddSeconds(2), mine.ArmedAt);
        });
    }

    [Fact]
    public void Mines_StayUntilTouched_EvenAfterTheNextFieldIsScheduled()
    {
        var runner = Fields();
        runner.Update(_state);
        Pass(runner.Update, 30);
        Pass(runner.Update, 20);

        Assert.Equal(4, _state.Mines.Count);
    }

    [Fact]
    public void Mines_AnotherFieldAddsToTheOnesThatAreStillThere()
    {
        var runner = Fields();
        runner.Update(_state);
        Pass(runner.Update, 30);
        Pass(runner.Update, 30);

        Assert.Equal(8, _state.Mines.Count);
    }

    [Fact]
    public void Mines_NeverMoreThanTheMaximumOnTheMapAtOnce()
    {
        var runner = Fields(Field with { MinMines = 40, MaxMines = 40 });
        runner.Update(_state);
        foreach (var _ in Enumerable.Range(0, 5)) Pass(runner.Update, 30);

        Assert.Equal(WorldEventRules.MaxActiveMines, _state.Mines.Count);
    }

    [Fact]
    public void Mines_AreResolvedEveryFrame()
    {
        var runner = Fields();
        runner.Update(_state);
        Pass(runner.Update, 1);
        Pass(runner.Update, 1);

        _gameStates.Received(3).ResolveMines(_state);
    }

    [Fact]
    public void Mines_ARoomWithoutPlayersLosesThem()
    {
        var runner = Fields();
        runner.Update(_state);
        Pass(runner.Update, 30);
        _state.Players.Clear();

        runner.Update(_state);

        Assert.Empty(_state.Mines);
    }

    [Fact]
    public void Mines_SwitchedOffTakesThemAllAway()
    {
        var rules = Rules(Quiet(o => o with { Mines = Field }));
        var runner = new MineFieldRunner(rules, new EventSchedule(() => 0.5), _gameStates, _time);
        runner.Update(_state);
        Pass(runner.Update, 30);

        rules.Reconfigure(Quiet(o => o with { Mines = Field with { Enabled = false } }));
        runner.Update(_state);

        Assert.Empty(_state.Mines);
    }

    [Fact]
    public void Mines_TriggerLaysAFieldAtOnce_EvenAfterOneWasLaid()
    {
        var runner = Fields();
        runner.Update(_state);
        Pass(runner.Update, 30);

        Assert.True(runner.Trigger(_state));
        runner.Update(_state);

        Assert.Equal(8, _state.Mines.Count);
    }
}
