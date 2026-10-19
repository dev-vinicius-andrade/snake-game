using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class ZoneRunnerTests
{
    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Half the diagonal of a room 1000 x 500.</summary>
    private static readonly double Cover = Math.Sqrt(500 * 500 + 250 * 250);

    private readonly FakeTime _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IGameStateService _gameStates = Substitute.For<IGameStateService>();
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly GameState _state;

    public ZoneRunnerTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1000;
        _room.Height = 500;
        _gameStates.ApplyZoneDamage(default!, default, default, default, default).ReturnsForAnyArgs([]);
        _state = new GameState(_room);
        AddPlayer("ana");
        AddPlayer("bo");
    }

    private static WorldEventOptions Options(Func<ZoneOptions, ZoneOptions>? change = null)
    {
        var zone = new ZoneOptions
        {
            MinIntervalSeconds = 30, MaxIntervalSeconds = 30, WarningSeconds = 10, ShrinkSeconds = 60, HoldSeconds = 30,
            FinalRadius = 100, DamageIntervalMilliseconds = 1000, DamageMass = 4, MinPlayers = 2
        };
        return new WorldEventOptions
        {
            Zone = change?.Invoke(zone) ?? zone,
            Lightning = new LightningOptions { Enabled = false },
            Meteors = new MeteorOptions { Enabled = false },
            Turret = new TurretOptions { Enabled = false }
        };
    }

    private ZoneRunner Runner(WorldEventOptions? options = null)
        => new(new WorldEventRules(options ?? Options(), () => 0.5), _gameStates, _time);

    private void AddPlayer(string name)
    {
        var player = new Player(Guid.NewGuid(), name) { CurrentPosition = new PointAxis { X = 10, Y = 10 } };
        _state.Players.Add(player.Guid, player);
    }

    private IReadOnlyList<IPlayer> Pass(ZoneRunner runner, double seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        return runner.Update(_state);
    }

    /// <summary>Through the first look and the wait: the zone has just begun, in its warning.</summary>
    private ZoneRunner Begun(WorldEventOptions? options = null)
    {
        var runner = Runner(options);
        runner.Update(_state);
        Pass(runner, 30);
        return runner;
    }

    [Fact]
    public void Update_BeforeTheFirstInterval_HasNoZone()
    {
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 29);

        Assert.Null(_state.Zone);
    }

    [Fact]
    public void Update_WhenDue_ShowsTheZoneInItsWarningCoveringTheRoom()
    {
        var runner = Begun();

        var zone = _state.Zone!;
        Assert.NotNull(zone);
        Assert.Equal(WorldZonePhase.Warning, zone.Phase);
        Assert.Equal((500.0, 250.0), (zone.X, zone.Y));
        Assert.Equal(Cover, zone.Radius, 6);
        Assert.Equal(100.0, zone.FinalRadius);
        Assert.Equal(_time.Now.AddSeconds(10), zone.PhaseEndsAt);
        _gameStates.DidNotReceiveWithAnyArgs().ApplyZoneDamage(default!, default, default, default, default);
        Assert.NotNull(runner);
    }

    [Fact]
    public void Update_ARoomThatSwitchedTheZoneOff_HasNone()
    {
        _room.Gameplay.Returns(new RoomGameplay { Zone = FeaturePreset.Off });
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 100);

        Assert.Null(_state.Zone);
    }

    [Fact]
    public void Update_ARoomThatPickedTheZoneWild_ClosesSoonerToASmallerCircle()
    {
        _room.Gameplay.Returns(new RoomGameplay { Zone = FeaturePreset.Wild });
        Begun(Options());

        Assert.True(_state.Zone!.FinalRadius < 100);
    }

    [Fact]
    public void Update_ARoomThatPickedTheZoneOn_HasOneWhereTheAdminSwitchedItOff()
    {
        _room.Gameplay.Returns(new RoomGameplay { Zone = FeaturePreset.Normal });
        var runner = Runner(Options(zone => zone with { Enabled = false }));
        runner.Update(_state);

        Pass(runner, 30);

        Assert.NotNull(_state.Zone);
    }

    [Fact]
    public void Update_TooFewPlayers_HasNoZone()
    {
        _state.Players.Remove(_state.Players.Keys.First());
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 100);

        Assert.Null(_state.Zone);
    }

    [Fact]
    public void Update_NoOneIsHurtInTheWarning_ThenSnakesOutsidePayEverySecondAtTheCurrentRadius()
    {
        var runner = Begun();
        Pass(runner, 9);
        _gameStates.DidNotReceiveWithAnyArgs().ApplyZoneDamage(default!, default, default, default, default);

        Pass(runner, 1);
        _gameStates.Received(1).ApplyZoneDamage(_state, 500, 250, Arg.Is<double>(radius => Math.Abs(radius - Cover) < 0.01), 4);

        Pass(runner, 0.5);
        _gameStates.Received(1).ApplyZoneDamage(Arg.Any<IGameState>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<long>());
        Pass(runner, 0.5);
        _gameStates.Received(2).ApplyZoneDamage(Arg.Any<IGameState>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<long>());
    }

    [Fact]
    public void Update_TheZoneClosesEvenlyThenHolds()
    {
        var runner = Begun();

        Pass(runner, 10);
        Assert.Equal(WorldZonePhase.Closing, _state.Zone!.Phase);
        Pass(runner, 30);
        Assert.Equal((Cover + 100) / 2, _state.Zone!.Radius, 6);
        Pass(runner, 30);

        Assert.Equal(WorldZonePhase.Holding, _state.Zone!.Phase);
        Assert.Equal(100.0, _state.Zone!.Radius, 6);
    }

    [Fact]
    public void Update_GivesBackTheSnakesTheZoneKilled()
    {
        var victim = _state.Players.Values.First();
        _gameStates.ApplyZoneDamage(Arg.Any<IGameState>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<long>()).Returns(new IPlayer[] { victim });
        var runner = Begun();

        var died = Pass(runner, 10);

        Assert.Same(victim, Assert.Single(died));
    }

    [Fact]
    public void Update_WhenTheHoldIsOver_TheZoneGoesAndTheNextComesAfterTheInterval()
    {
        var runner = Begun();

        Pass(runner, 100);
        Assert.Null(_state.Zone);

        Pass(runner, 29);
        Assert.Null(_state.Zone);
        Pass(runner, 1);
        Assert.NotNull(_state.Zone);
    }

    [Fact]
    public void Update_ARoomWithoutPlayers_LosesItsZone()
    {
        var runner = Begun();

        foreach (var key in _state.Players.Keys.ToArray()) _state.Players.Remove(key);
        Pass(runner, 0.5);

        Assert.Null(_state.Zone);
    }

    [Fact]
    public void Update_ARoomItIsNotFor_HasNoZone()
    {
        _room.Code.Returns("calm");
        var runner = Runner(Options(zone => zone with { ExcludedRooms = ["calm"] }));
        runner.Update(_state);

        Pass(runner, 100);

        Assert.Null(_state.Zone);
    }

    [Fact]
    public void Update_SwitchedOffWhileItShows_TakesItAwayWithoutHurtingAnyone()
    {
        var rules = new WorldEventRules(Options(), () => 0.5);
        var runner = new ZoneRunner(rules, _gameStates, _time);
        runner.Update(_state);
        Pass(runner, 30);
        Assert.NotNull(_state.Zone);

        rules.Reconfigure(Options(zone => zone with { Enabled = false }));
        Pass(runner, 20);

        Assert.Null(_state.Zone);
        _gameStates.DidNotReceiveWithAnyArgs().ApplyZoneDamage(default!, default, default, default, default);
    }

    [Fact]
    public void Update_RoomsAreIndependent()
    {
        var other = Substitute.For<IRoom>();
        other.Guid.Returns(Guid.NewGuid());
        other.Width = 1000;
        other.Height = 500;
        var otherState = new GameState(other);
        foreach (var name in new[] { "x", "y" })
        {
            var player = new Player(Guid.NewGuid(), name) { CurrentPosition = new PointAxis { X = 1, Y = 1 } };
            otherState.Players.Add(player.Guid, player);
        }
        var runner = Runner();
        runner.Update(_state);
        _time.Now = _time.Now.AddSeconds(10);
        runner.Update(otherState);

        _time.Now = _time.Now.AddSeconds(20);
        runner.Update(_state);
        runner.Update(otherState);

        Assert.NotNull(_state.Zone);
        Assert.Null(otherState.Zone);
    }
}
