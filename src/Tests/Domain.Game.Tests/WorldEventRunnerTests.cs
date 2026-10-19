using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
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

    /// <summary>Lightning: a storm of exactly 3 strikes beginning at once, every 30 s, each warned for 1.5 s. Meteors off unless given.</summary>
    private static WorldEventOptions Options(Func<LightningOptions, LightningOptions>? change = null, MeteorOptions? meteors = null)
    {
        var lightning = new LightningOptions
        {
            MinIntervalSeconds = 30, MaxIntervalSeconds = 30, Radius = 80, TelegraphMilliseconds = 1500,
            MinStrikes = 3, MaxStrikes = 3, StormSeconds = 1
        };
        return new WorldEventOptions { Lightning = change?.Invoke(lightning) ?? lightning, Meteors = meteors ?? new MeteorOptions { Enabled = false } };
    }

    private static MeteorOptions OneMeteor() => new()
    {
        MinIntervalSeconds = 30, MaxIntervalSeconds = 30, Radius = 130, TelegraphMilliseconds = 2000, MinMeteors = 1, MaxMeteors = 1, RainSeconds = 1
    };

    private WorldEventRunner Runner(WorldEventOptions? options = null, double random = 0.5)
        => new(new WorldEventRules(options ?? Options(), () => random), _gameStates, _time);

    private Player AddPlayer(string name = "ana")
    {
        var player = new Player(Guid.NewGuid(), name) { CurrentPosition = new PointAxis { X = 10, Y = 10 } };
        _state.Players.Add(player.Guid, player);
        return player;
    }

    private void Pass(WorldEventRunner runner, double seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        runner.Update(_state);
    }

    /// <summary>Gets a runner through its first look and the wait, so the storm has begun (every strike marked, as the draws are 0).</summary>
    private WorldEventRunner Stormy(WorldEventOptions? options = null)
    {
        var runner = Runner(options, random: 0);
        runner.Update(_state);
        Pass(runner, 30);
        return runner;
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
    public void Update_WhenDue_ShowsEveryStrikeOfTheStormWithoutStrikingYet()
    {
        AddPlayer();

        Stormy();

        Assert.Equal(3, _state.Strikes.Count);
        Assert.All(_state.Strikes.Values, strike =>
        {
            Assert.Equal(80, strike.Radius);
            Assert.Equal(WorldStrikeKind.Lightning, strike.Kind);
            Assert.Equal(_time.Now.AddMilliseconds(1500), strike.StrikesAt);
        });
        _gameStates.DidNotReceiveWithAnyArgs().StrikeArea(default!, default, default, default);
    }

    [Fact]
    public void Update_WhenTheWarningsRunOut_StrikesEverySpotAndTakesThemDown()
    {
        AddPlayer();
        var runner = Stormy();

        Pass(runner, 1.5);

        Assert.Empty(_state.Strikes);
        _gameStates.Received(3).StrikeArea(_state, Arg.Any<double>(), Arg.Any<double>(), 80);
    }

    [Fact]
    public void Update_StrikesThatBeginAtDifferentMoments_FallOnePerWarning()
    {
        AddPlayer();
        var options = Options(lightning => lightning with { StormSeconds = 4 });
        var runner = Runner(options, random: 0.5);
        runner.Update(_state);
        Pass(runner, 30);
        Assert.Empty(_state.Strikes);

        Pass(runner, 2);
        Assert.Equal(3, _state.Strikes.Count);
        Pass(runner, 1.5);

        Assert.Empty(_state.Strikes);
        _gameStates.Received(3).StrikeArea(Arg.Any<IGameState>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>());
    }

    [Fact]
    public void Update_GivesBackTheSnakesTheStrikesKilled()
    {
        var victim = AddPlayer();
        var calls = 0;
        _gameStates.StrikeArea(Arg.Any<IGameState>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(_ => ++calls == 1 ? new IPlayer[] { victim } : []);
        var runner = Stormy();
        _time.Now = _time.Now.AddSeconds(1.5);

        var died = runner.Update(_state);

        Assert.Same(victim, Assert.Single(died));
    }

    [Fact]
    public void Update_AfterAStorm_AnotherComesAfterTheInterval()
    {
        AddPlayer();
        var runner = Stormy();
        Pass(runner, 1.5);

        Pass(runner, 29);
        Assert.Empty(_state.Strikes);

        Pass(runner, 1);
        Assert.Equal(3, _state.Strikes.Count);
    }

    [Fact]
    public void Update_AimedStrikes_GoNearTheLivingSnakes()
    {
        var player = AddPlayer();
        player.CurrentPosition = new PointAxis { X = 400, Y = 200 };
        var options = Options(lightning => lightning with { AimedShare = 1 });
        var runner = Runner(options, random: 0);
        runner.Update(_state);

        Pass(runner, 30);

        Assert.All(_state.Strikes.Values, strike => Assert.Equal((400.0, 200.0), (strike.X, strike.Y)));
    }

    [Fact]
    public void Update_ARoomWithoutPlayers_ShowsNothingAndForgetsTheSchedule()
    {
        var player = AddPlayer();
        var runner = Stormy();
        Assert.NotEmpty(_state.Strikes);

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
    public void Update_SwitchedOffWhileWarningsShow_TakesThemDownWithoutStriking()
    {
        AddPlayer();
        var rules = new WorldEventRules(Options(), () => 0);
        var runner = new WorldEventRunner(rules, _gameStates, _time);
        runner.Update(_state);
        Pass(runner, 30);
        Assert.NotEmpty(_state.Strikes);

        rules.Reconfigure(Options() with { Enabled = false });
        Pass(runner, 1);

        Assert.Empty(_state.Strikes);
        _gameStates.DidNotReceiveWithAnyArgs().StrikeArea(default!, default, default, default);
    }

    [Fact]
    public void Update_Meteors_AreShownAsMeteorsWithTheirOwnRadiusAndWarning()
    {
        AddPlayer();
        var options = Options(lightning => lightning with { Enabled = false }, OneMeteor());
        var runner = Runner(options, random: 0);
        runner.Update(_state);

        Pass(runner, 30);

        var meteor = Assert.Single(_state.Strikes.Values);
        Assert.Equal((WorldStrikeKind.Meteor, 130.0), (meteor.Kind, meteor.Radius));
        Assert.Equal(_time.Now.AddMilliseconds(2000), meteor.StrikesAt);
    }

    [Fact]
    public void Update_MeteorsStrikeWithTheSameCodeAsLightning()
    {
        AddPlayer();
        var options = Options(lightning => lightning with { Enabled = false }, OneMeteor());
        var runner = Runner(options, random: 0);
        runner.Update(_state);
        Pass(runner, 30);

        Pass(runner, 2);

        Assert.Empty(_state.Strikes);
        _gameStates.Received(1).StrikeArea(_state, Arg.Any<double>(), Arg.Any<double>(), 130);
    }

    [Fact]
    public void Update_LightningAndMeteorsRunTogetherWithoutDisturbingEachOther()
    {
        AddPlayer();
        var runner = Runner(Options(meteors: OneMeteor()), random: 0);
        runner.Update(_state);

        Pass(runner, 30);

        Assert.Equal(3, _state.Strikes.Values.Count(strike => strike.Kind == WorldStrikeKind.Lightning));
        Assert.Equal(1, _state.Strikes.Values.Count(strike => strike.Kind == WorldStrikeKind.Meteor));
        Pass(runner, 1.5);
        Assert.Equal(1, _state.Strikes.Values.Count(strike => strike.Kind == WorldStrikeKind.Meteor));
        Pass(runner, 0.5);
        Assert.Empty(_state.Strikes);
    }

    [Fact]
    public void Update_NeverShowsMoreThanTheCapAtOnce_AndStrikesOnlyWhatWasShown()
    {
        AddPlayer();
        var options = Options(lightning => lightning with { MinStrikes = 40, MaxStrikes = 40 }, OneMeteor());
        var runner = Runner(options, random: 0);
        runner.Update(_state);

        Pass(runner, 30);

        Assert.Equal(WorldEventRunner.MaxActiveStrikes, _state.Strikes.Count);
        Pass(runner, 2);
        Assert.Empty(_state.Strikes);
        _gameStates.Received(WorldEventRunner.MaxActiveStrikes).StrikeArea(Arg.Any<IGameState>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>());
    }

    [Fact]
    public void Update_ARoomThatSwitchedLightningOff_HasNone_WhateverTheAdminSays()
    {
        AddPlayer();
        _room.Gameplay.Returns(new RoomGameplay { Lightning = FeaturePreset.Off });
        var runner = Runner(Options(meteors: OneMeteor()), random: 0);
        runner.Update(_state);

        Pass(runner, 30);

        Assert.All(_state.Strikes.Values, strike => Assert.Equal(WorldStrikeKind.Meteor, strike.Kind));
        Assert.Single(_state.Strikes);
    }

    [Fact]
    public void Update_ARoomThatPickedLightningWild_GetsMoreStrikesOfItThanTheAdminAsked()
    {
        AddPlayer();
        _room.Gameplay.Returns(new RoomGameplay { Lightning = FeaturePreset.Wild });
        var runner = Runner(Options(), random: 0);
        runner.Update(_state);

        Pass(runner, 30);
        Pass(runner, 0.1);

        // The admin asked for exactly 3 strikes, all at once; wild makes it 5 and brings the wait down, so it is due sooner.
        Assert.True(_state.Strikes.Count > 3);
    }

    [Fact]
    public void Update_ARoomThatPickedLightningOn_HasItEvenWhereTheAdminSwitchedItOff()
    {
        AddPlayer();
        _room.Gameplay.Returns(new RoomGameplay { Lightning = FeaturePreset.Normal });
        var runner = Runner(Options(lightning => lightning with { Enabled = false }), random: 0);
        runner.Update(_state);

        Pass(runner, 30);

        Assert.Equal(3, _state.Strikes.Count);
    }

    [Fact]
    public void Trigger_StartsAStormAtOnce_WithoutWaitingForTheInterval()
    {
        AddPlayer();
        var runner = Runner(random: 0);
        runner.Update(_state);

        Assert.True(runner.Trigger(_state, WorldEventKind.Lightning));
        Pass(runner, 0.1);

        Assert.Equal(3, _state.Strikes.Count);
    }

    [Fact]
    public void Trigger_AStormThatIsAlreadyGoing_IsLeftAlone()
    {
        AddPlayer();
        var runner = Runner(random: 0);
        runner.Update(_state);
        Pass(runner, 30);

        Assert.False(runner.Trigger(_state, WorldEventKind.Lightning));
    }

    [Fact]
    public void Trigger_AnEventThatIsOffInTheRoom_RefusesAndStartsNothing()
    {
        AddPlayer();
        var runner = Runner(Options(lightning => lightning with { Enabled = false }), random: 0);
        runner.Update(_state);

        Assert.False(runner.Trigger(_state, WorldEventKind.Lightning));
        Pass(runner, 1);

        Assert.Empty(_state.Strikes);
    }

    [Fact]
    public void Trigger_ARoomWithNoPlayers_RefusesAndStartsNothing()
    {
        var runner = Runner(random: 0);

        Assert.False(runner.Trigger(_state, WorldEventKind.Lightning));
    }

    [Fact]
    public void Trigger_OnlyTheKindAsked_Starts()
    {
        AddPlayer();
        var runner = Runner(Options(meteors: OneMeteor()), random: 0);
        runner.Update(_state);

        runner.Trigger(_state, WorldEventKind.Meteor);
        Pass(runner, 0.1);

        Assert.All(_state.Strikes.Values, strike => Assert.Equal(WorldStrikeKind.Meteor, strike.Kind));
        Assert.Single(_state.Strikes);
    }

    [Fact]
    public void Update_OneKindSwitchedOff_LeavesTheOthersWarningsUp()
    {
        AddPlayer();
        var rules = new WorldEventRules(Options(meteors: OneMeteor()), () => 0);
        var runner = new WorldEventRunner(rules, _gameStates, _time);
        runner.Update(_state);
        Pass(runner, 30);

        rules.Reconfigure(Options(lightning => lightning with { Enabled = false }, OneMeteor()));
        Pass(runner, 0.1);

        Assert.All(_state.Strikes.Values, strike => Assert.Equal(WorldStrikeKind.Meteor, strike.Kind));
        Assert.Single(_state.Strikes);
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
        var stranger = new Player(Guid.NewGuid(), "bo") { CurrentPosition = new PointAxis { X = 10, Y = 10 } };
        otherState.Players.Add(stranger.Guid, stranger);
        var runner = Runner(random: 0);
        runner.Update(_state);
        _time.Now = _time.Now.AddSeconds(10);
        runner.Update(otherState);

        _time.Now = _time.Now.AddSeconds(20);
        runner.Update(_state);
        runner.Update(otherState);

        Assert.NotEmpty(_state.Strikes);
        Assert.Empty(otherState.Strikes);
    }
}
