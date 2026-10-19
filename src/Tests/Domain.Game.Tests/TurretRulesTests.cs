using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class TurretRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const double Width = 1000;
    private const double Height = 500;

    private static TurretOptions Turret(Func<TurretOptions, TurretOptions>? change = null)
    {
        var turret = new TurretOptions
        {
            MinIntervalSeconds = 100, MaxIntervalSeconds = 200, LifetimeSeconds = 60, HitPoints = 20, Size = 60,
            FireIntervalMilliseconds = 1000, Arms = 4, SpinDegrees = 10, ShotSpeed = 0.5, MinPlayers = 2, RewardMass = 50
        };
        return change?.Invoke(turret) ?? turret;
    }

    private static WorldEventRules Rules(TurretOptions? turret = null, double random = 0)
        => new(new WorldEventOptions { Turret = turret ?? Turret() }, () => random);

    private static TurretStep Step(WorldEventRules rules, TurretState state, double seconds, int players = 3)
        => rules.TurretStep(state, Start.AddSeconds(seconds), Width, Height, players);

    private static TurretState Waiting(double spawnAtSeconds = 100) => new(Start.AddSeconds(spawnAtSeconds), null, null, 0, 0, 0);

    private static TurretState Active(double expiresAt = 60, double fireAt = 10, double angle = 0)
        => new(null, Start.AddSeconds(expiresAt), Start.AddSeconds(fireAt), angle, 500, 250);

    [Fact]
    public void TurretStep_FirstLook_SchedulesTheFirstOneWithinTheInterval()
    {
        var step = Step(Rules(random: 0.5), TurretState.None, 0);

        Assert.Equal(TurretAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(150), step.State.NextSpawnAt);
        Assert.False(step.State.IsActive);
    }

    [Fact]
    public void TurretStep_BeforeItIsDue_DoesNothing()
    {
        var state = Waiting();

        var step = Step(Rules(), state, 99);

        Assert.Equal(TurretAction.None, step.Action);
        Assert.Same(state, step.State);
    }

    [Fact]
    public void TurretStep_TooFewPlayers_WaitsWithoutSpawning()
    {
        var state = Waiting();

        var step = Step(Rules(), state, 500, players: 1);

        Assert.Equal(TurretAction.None, step.Action);
        Assert.Same(state, step.State);
    }

    [Fact]
    public void TurretStep_WhenDue_SpawnsInsideTheRoomAndGivesPlayersTimeBeforeItFires()
    {
        var step = Step(Rules(random: 0.5), Waiting(), 100);

        Assert.Equal(TurretAction.Spawn, step.Action);
        Assert.True(step.State.IsActive);
        Assert.Equal(Start.AddSeconds(160), step.State.ExpiresAt);
        Assert.Equal(Start.AddSeconds(100).Add(WorldEventRules.TurretWarmUp), step.State.NextFireAt);
        Assert.Equal((500.0, 250.0), (step.State.X, step.State.Y));
        Assert.Null(step.State.NextSpawnAt);
    }

    [Fact]
    public void TurretStep_SpawnsAtLeastAHalfSizeFromTheEdges()
    {
        var corner = Step(Rules(random: 0), Waiting(), 100).State;
        var far = Step(Rules(random: 1), Waiting(), 100).State;

        Assert.Equal((30.0, 30.0), (corner.X, corner.Y));
        Assert.Equal((970.0, 470.0), (far.X, far.Y));
    }

    [Fact]
    public void TurretStep_BeforeItFires_DoesNothing()
    {
        var step = Step(Rules(), Active(fireAt: 10), 9.9);

        Assert.Equal(TurretAction.None, step.Action);
    }

    [Fact]
    public void TurretStep_Fires_OneShotPerArmEvenlySpread()
    {
        var step = Step(Rules(), Active(fireAt: 10, angle: 0.5), 10);

        Assert.Equal(TurretAction.Fire, step.Action);
        Assert.Equal(4, step.Angles.Count);
        var expected = new[] { 0.5, 0.5 + Math.PI / 2, 0.5 + Math.PI, 0.5 + 3 * Math.PI / 2 };
        for (var arm = 0; arm < 4; arm++) Assert.Equal(expected[arm], step.Angles[arm], 9);
    }

    [Fact]
    public void TurretStep_AfterFiring_TurnsByTheSpinAndWaitsForTheInterval()
    {
        var step = Step(Rules(), Active(fireAt: 10, angle: 1), 10);

        Assert.Equal(1 + 10 * Math.PI / 180, step.State.Angle, 9);
        Assert.Equal(Start.AddSeconds(11), step.State.NextFireAt);
        Assert.Equal((500.0, 250.0), (step.State.X, step.State.Y));
    }

    [Fact]
    public void TurretStep_WhenItsTimeIsUp_ExpiresAndTheNextIsScheduled()
    {
        var step = Step(Rules(random: 0), Active(expiresAt: 60), 60);

        Assert.Equal(TurretAction.Expire, step.Action);
        Assert.False(step.State.IsActive);
        Assert.Equal(Start.AddSeconds(160), step.State.NextSpawnAt);
    }

    [Fact]
    public void TurretStep_ExpiringWinsOverFiring()
    {
        var step = Step(Rules(), Active(expiresAt: 60, fireAt: 59), 61);

        Assert.Equal(TurretAction.Expire, step.Action);
    }

    [Fact]
    public void TurretStep_StaysActiveWhenPlayersLeave_ItIsTheRoomThatForgetsIt()
    {
        var step = Step(Rules(), Active(fireAt: 10), 10, players: 0);

        Assert.Equal(TurretAction.Fire, step.Action);
    }

    [Fact]
    public void TurretKilled_SchedulesTheNextFromNow()
    {
        var state = Rules(random: 1).TurretKilled(Start.AddSeconds(30));

        Assert.False(state.IsActive);
        Assert.Equal(Start.AddSeconds(230), state.NextSpawnAt);
    }

    [Fact]
    public void TurretStep_SwitchedOff_ForgetsItAndDoesNothing()
    {
        var off = Rules(Turret(turret => turret with { Enabled = false }));

        var step = Step(off, Active(), 30);

        Assert.Equal(TurretAction.None, step.Action);
        Assert.Same(TurretState.None, step.State);
    }

    [Fact]
    public void TurretStep_AllEventsOff_ForgetsItAndDoesNothing()
    {
        var rules = new WorldEventRules(new WorldEventOptions { Enabled = false, Turret = Turret() });

        Assert.Same(TurretState.None, Step(rules, Active(), 30).State);
    }

    [Fact]
    public void Hits_AShotInsideTheBoxHitsAndOneOutsideDoesNot()
    {
        Assert.True(WorldEventRules.TurretIsHit(500, 250, 60, new Point(520, 260), 15));
        Assert.False(WorldEventRules.TurretIsHit(500, 250, 60, new Point(600, 250), 15));
    }

    [Fact]
    public void Hits_TheEdgeOfTheShotTouchingTheBoxCounts()
    {
        // The box reaches x = 530; a shot of 15 centred at 537.5 has its edge at 530.
        Assert.True(WorldEventRules.TurretIsHit(500, 250, 60, new Point(530, 250), 15));
        Assert.False(WorldEventRules.TurretIsHit(500, 250, 60, new Point(538, 250), 15));
    }

    [Theory]
    [InlineData(0, 10, 60, 1000, 4, 0.5, 2, 50, 20)]
    [InlineData(10, 5, 60, 1000, 4, 0.5, 2, 50, 20)]
    [InlineData(5, 10, 1, 1000, 4, 0.5, 2, 50, 20)]
    [InlineData(5, 10, 60, 10, 4, 0.5, 2, 50, 20)]
    [InlineData(5, 10, 60, 1000, 0, 0.5, 2, 50, 20)]
    [InlineData(5, 10, 60, 1000, 17, 0.5, 2, 50, 20)]
    [InlineData(5, 10, 60, 1000, 4, 0, 2, 50, 20)]
    [InlineData(5, 10, 60, 1000, 4, 0.5, 0, 50, 20)]
    [InlineData(5, 10, 60, 1000, 4, 0.5, 2, -1, 20)]
    [InlineData(5, 10, 60, 1000, 4, 0.5, 2, 50, 0)]
    public void Constructor_TurretOutOfRange_IsRefused(int min, int max, int lifetime, int fireInterval, int arms, double speed, int minPlayers, int reward, int hitPoints)
    {
        var turret = Turret(options => options with
        {
            MinIntervalSeconds = min, MaxIntervalSeconds = max, LifetimeSeconds = lifetime, FireIntervalMilliseconds = fireInterval,
            Arms = arms, ShotSpeed = speed, MinPlayers = minPlayers, RewardMass = reward, HitPoints = hitPoints
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventRules(new WorldEventOptions { Turret = turret }));
    }

    [Fact]
    public void Constructor_ABlankRoom_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new WorldEventRules(new WorldEventOptions { Turret = Turret(turret => turret with { Rooms = [" "] }) }));
    }
}
