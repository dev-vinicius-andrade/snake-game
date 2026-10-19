using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class TurretRunnerTests
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

    public TurretRunnerTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1000;
        _room.Height = 500;
        _state = new GameState(_room);
        AddPlayer("ana");
        AddPlayer("bo");
    }

    private static WorldEventOptions Options(Func<TurretOptions, TurretOptions>? change = null)
    {
        var turret = new TurretOptions
        {
            MinIntervalSeconds = 30, MaxIntervalSeconds = 30, LifetimeSeconds = 60, HitPoints = 20, Size = 60, FireIntervalMilliseconds = 1000,
            Arms = 4, SpinDegrees = 10, ShotSpeed = 0.5, MinPlayers = 2, RewardMass = 50, GrantsPerk = "twin"
        };
        return new WorldEventOptions
        {
            Turret = change?.Invoke(turret) ?? turret,
            Lightning = new LightningOptions { Enabled = false },
            Meteors = new MeteorOptions { Enabled = false }
        };
    }

    private TurretRunner Runner(WorldEventOptions? options = null)
        => new(new WorldEventRules(options ?? Options(), () => 0.5), _gameStates, _time);

    private void AddPlayer(string name)
    {
        var player = new Player(Guid.NewGuid(), name) { CurrentPosition = new PointAxis { X = 10, Y = 10 } };
        _state.Players.Add(player.Guid, player);
    }

    private void Pass(TurretRunner runner, double seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        runner.Update(_state);
    }

    /// <summary>Through the first look and the wait: the turret has just appeared.</summary>
    private TurretRunner Appeared(WorldEventOptions? options = null)
    {
        var runner = Runner(options);
        runner.Update(_state);
        Pass(runner, 30);
        return runner;
    }

    [Fact]
    public void Update_BeforeTheFirstInterval_HasNoBoss()
    {
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 29);

        Assert.Null(_state.Boss);
    }

    [Fact]
    public void Update_WhenDue_PutsTheTurretInTheRoomWithWhatTheSettingsSay()
    {
        Appeared();

        var boss = _state.Boss!;
        Assert.NotNull(boss);
        Assert.Equal((20.0, 20, 60.0, 4, 0.5, 50L, "twin"), (boss.Hp, boss.MaxHp, boss.Size, boss.Arms, boss.ShotSpeed, boss.RewardMass, boss.GrantsPerk));
        Assert.Equal((500.0, 250.0), (boss.X, boss.Y));
        Assert.NotEqual(Guid.Empty, boss.Id);
    }

    [Fact]
    public void Update_TooFewPlayers_HasNoBoss()
    {
        _state.Players.Remove(_state.Players.Keys.First());
        var runner = Runner();
        runner.Update(_state);

        Pass(runner, 100);

        Assert.Null(_state.Boss);
    }

    [Fact]
    public void Update_GivesPlayersTimeBeforeTheFirstVolley_ThenFiresOnTheInterval()
    {
        var runner = Appeared();
        Pass(runner, 2.9);
        _gameStates.DidNotReceiveWithAnyArgs().FireTurret(default!, default!);

        Pass(runner, 0.1);
        _gameStates.Received(1).FireTurret(_state, Arg.Is<IReadOnlyList<double>>(angles => angles.Count == 4));

        Pass(runner, 0.5);
        _gameStates.Received(1).FireTurret(Arg.Any<IGameState>(), Arg.Any<IReadOnlyList<double>>());
        Pass(runner, 0.5);
        _gameStates.Received(2).FireTurret(Arg.Any<IGameState>(), Arg.Any<IReadOnlyList<double>>());
    }

    [Fact]
    public void Update_TheTurretTurnsAsItFires()
    {
        var runner = Appeared();
        var before = _state.Boss!.Angle;

        Pass(runner, 3);

        Assert.Equal(before + 10 * Math.PI / 180, _state.Boss!.Angle, 9);
    }

    [Fact]
    public void Update_WhenItsTimeIsUp_TheTurretLeavesAndTheNextComesAfterTheInterval()
    {
        var runner = Appeared();

        Pass(runner, 60);
        Assert.Null(_state.Boss);

        Pass(runner, 29);
        Assert.Null(_state.Boss);
        Pass(runner, 1);
        Assert.NotNull(_state.Boss);
    }

    [Fact]
    public void Update_WhenSnakesBringItDown_TheNextComesAfterTheIntervalFromThen()
    {
        var runner = Appeared();
        Pass(runner, 10);

        _state.Boss = null;
        Pass(runner, 0.1);
        Assert.Null(_state.Boss);

        Pass(runner, 29);
        Assert.Null(_state.Boss);
        Pass(runner, 1);
        Assert.NotNull(_state.Boss);
    }

    [Fact]
    public void Update_ARoomWithoutPlayers_LosesItsTurret()
    {
        var runner = Appeared();

        foreach (var key in _state.Players.Keys.ToArray()) _state.Players.Remove(key);
        Pass(runner, 0.5);

        Assert.Null(_state.Boss);
    }

    [Fact]
    public void Update_ARoomItIsNotFor_HasNoTurret()
    {
        _room.Code.Returns("calm");
        var runner = Runner(Options(turret => turret with { ExcludedRooms = ["calm"] }));
        runner.Update(_state);

        Pass(runner, 100);

        Assert.Null(_state.Boss);
    }

    [Fact]
    public void Update_SwitchedOffWhileItStands_TakesItAway()
    {
        var rules = new WorldEventRules(Options(), () => 0.5);
        var runner = new TurretRunner(rules, _gameStates, _time);
        runner.Update(_state);
        Pass(runner, 30);
        Assert.NotNull(_state.Boss);

        rules.Reconfigure(Options(turret => turret with { Enabled = false }));
        Pass(runner, 1);

        Assert.Null(_state.Boss);
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

        Assert.NotNull(_state.Boss);
        Assert.Null(otherState.Boss);
    }
}
