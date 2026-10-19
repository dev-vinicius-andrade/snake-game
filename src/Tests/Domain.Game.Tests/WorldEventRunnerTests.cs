using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class WorldEventRunnerTests
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

    public WorldEventRunnerTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1000;
        _room.Height = 500;
        _gameStates.StrikeArea(default!, default, default, default).ReturnsForAnyArgs([]);
        _state = new GameState(_room);
    }

    private static WorldEventOptions Options(Func<LightningOptions, LightningOptions>? change = null)
    {
        var lightning = new LightningOptions { MinIntervalSeconds = 30, MaxIntervalSeconds = 30, Radius = 80, TelegraphMilliseconds = 1500 };
        return new WorldEventOptions { Lightning = change?.Invoke(lightning) ?? lightning };
    }

    private WorldEventRunner Runner(WorldEventOptions? options = null, double random = 0.5)
        => new(new WorldEventRules(options ?? Options(), () => random), _gameStates, _time);

    private Player AddPlayer(string name = "ana")
    {
        var player = new Player(Guid.NewGuid(), name);
        _state.Players.Add(player.Guid, player);
        return player;
    }

    private void Pass(WorldEventRunner runner, double seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        runner.Update(_state);
    }

    [Fact]
    public void Update_BeforeTheFirstInterval_ShowsNothing()
    {
        AddPlayer();
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 29);

        Assert.Empty(_state.Strikes);
    }

    [Fact]
    public void Update_WhenDue_ShowsTheSpotWithoutStrikingYet()
    {
        AddPlayer();
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 30);

        var strike = Assert.Single(_state.Strikes.Values);
        Assert.Equal((500, 250, 80), (strike.X, strike.Y, strike.Radius));
        Assert.Equal(_time.Now.AddMilliseconds(1500), strike.StrikesAt);
        _gameStates.DidNotReceiveWithAnyArgs().StrikeArea(default!, default, default, default);
    }

    [Fact]
    public void Update_WhenTheWarningRunsOut_StrikesTheSpotAndTakesItDown()
    {
        AddPlayer();
        var runner = Runner();
        runner.Update(_state);
        Pass(runner, 30);

        Pass(runner, 1.5);

        Assert.Empty(_state.Strikes);
        _gameStates.Received(1).StrikeArea(_state, 500, 250, 80);
    }

    [Fact]
    public void Update_GivesBackTheSnakesTheStrikeKilled()
    {
        var victim = AddPlayer();
        _gameStates.StrikeArea(_state, 500, 250, 80).Returns([victim]);
        var runner = Runner();
        runner.Update(_state);
        Pass(runner, 30);
        _time.Now = _time.Now.AddSeconds(1.5);

        var died = runner.Update(_state);

        Assert.Same(victim, Assert.Single(died));
    }

    [Fact]
    public void Update_AfterAStrike_AnotherComesAfterTheInterval()
    {
        AddPlayer();
        var runner = Runner();
        runner.Update(_state);
        Pass(runner, 30);
        Pass(runner, 1.5);

        Pass(runner, 29);
        Assert.Empty(_state.Strikes);

        Pass(runner, 1);
        Assert.Single(_state.Strikes);
    }

    [Fact]
    public void Update_ARoomWithoutPlayers_ShowsNothingAndForgetsTheSchedule()
    {
        var player = AddPlayer();
        var runner = Runner();
        runner.Update(_state);
        Pass(runner, 30);
        Assert.Single(_state.Strikes);

        _state.Players.Remove(player.Guid);
        Pass(runner, 0.5);

        Assert.Empty(_state.Strikes);
        _gameStates.DidNotReceiveWithAnyArgs().StrikeArea(default!, default, default, default);
    }

    [Fact]
    public void Update_ARoomItDoesNotStrikeIn_DoesNothing()
    {
        AddPlayer();
        _room.Code.Returns("calm");
        var runner = Runner(Options(lightning => lightning with { ExcludedRooms = ["calm"] }));
        runner.Update(_state);

        Pass(runner, 100);

        Assert.Empty(_state.Strikes);
    }

    [Fact]
    public void Update_SwitchedOffWhileTheWarningShows_TakesTheWarningDownWithoutStriking()
    {
        AddPlayer();
        var rules = new WorldEventRules(Options(), () => 0.5);
        var runner = new WorldEventRunner(rules, _gameStates, _time);
        runner.Update(_state);
        Pass(runner, 30);
        Assert.Single(_state.Strikes);

        rules.Reconfigure(Options() with { Enabled = false });
        Pass(runner, 1);

        Assert.Empty(_state.Strikes);
        _gameStates.DidNotReceiveWithAnyArgs().StrikeArea(default!, default, default, default);
    }

    [Fact]
    public void Update_RoomsAreIndependent()
    {
        AddPlayer();
        var other = Substitute.For<IRoom>();
        other.Guid.Returns(Guid.NewGuid());
        other.Width = 1000;
        other.Height = 500;
        var otherState = new GameState(other);
        var stranger = new Player(Guid.NewGuid(), "bo");
        otherState.Players.Add(stranger.Guid, stranger);
        var runner = Runner();
        runner.Update(_state);
        _time.Now = _time.Now.AddSeconds(10);
        runner.Update(otherState);

        _time.Now = _time.Now.AddSeconds(20);
        runner.Update(_state);
        runner.Update(otherState);

        Assert.Single(_state.Strikes);
        Assert.Empty(otherState.Strikes);
    }
}
