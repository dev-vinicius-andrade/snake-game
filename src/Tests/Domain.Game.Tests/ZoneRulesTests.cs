using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class ZoneRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const double Width = 600;
    private const double Height = 800;
    /// <summary>Half the diagonal of the room: far enough that the zone covers all of it, even across the edges.</summary>
    private const double StartRadius = 500;

    private static ZoneOptions Zone(Func<ZoneOptions, ZoneOptions>? change = null)
    {
        var zone = new ZoneOptions
        {
            MinIntervalSeconds = 100, MaxIntervalSeconds = 200, WarningSeconds = 10, ShrinkSeconds = 60, HoldSeconds = 30,
            FinalRadius = 100, DamageIntervalMilliseconds = 1000, DamageMass = 4, MinPlayers = 2
        };
        return change?.Invoke(zone) ?? zone;
    }

    private static WorldEventRules Rules(ZoneOptions? zone = null, double random = 0)
        => new(new WorldEventOptions { Zone = zone ?? Zone() }, () => random);

    private static ZoneStep Step(WorldEventRules rules, ZoneState state, double seconds, int players = 3)
        => rules.ZoneStep(state, Start.AddSeconds(seconds), Width, Height, players);

    private static ZoneState Waiting(double spawnAtSeconds = 100) => new(Start.AddSeconds(spawnAtSeconds), null, null, null, null, 0, 0, 0);

    /// <summary>A zone that began at 0 s: warning until 10, closing until 70, holding until 100.</summary>
    private static ZoneState Active() => new(
        null, Start.AddSeconds(10), Start.AddSeconds(70), Start.AddSeconds(100), Start.AddSeconds(10), 300, 400, StartRadius);

    [Fact]
    public void ZoneStep_FirstLook_SchedulesTheFirstOneWithinTheInterval()
    {
        var step = Step(Rules(random: 0.5), ZoneState.None, 0);

        Assert.Equal(ZoneAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(150), step.State.NextAt);
        Assert.False(step.State.IsActive);
    }

    [Fact]
    public void ZoneStep_BeforeItIsDue_OrWithTooFewPlayers_DoesNothing()
    {
        var state = Waiting();

        Assert.Same(state, Step(Rules(), state, 99).State);
        Assert.Same(state, Step(Rules(), state, 500, players: 1).State);
        Assert.Equal(ZoneAction.None, Step(Rules(), state, 500, players: 1).Action);
    }

    [Fact]
    public void ZoneStep_WhenDue_BeginsWithAWarningThenClosingThenHolding()
    {
        var step = Step(Rules(random: 0.5), Waiting(), 100);

        Assert.Equal(ZoneAction.Begin, step.Action);
        var state = step.State;
        Assert.True(state.IsActive);
        Assert.Equal(Start.AddSeconds(110), state.ClosingAt);
        Assert.Equal(Start.AddSeconds(170), state.ClosedAt);
        Assert.Equal(Start.AddSeconds(200), state.EndsAt);
        Assert.Equal(Start.AddSeconds(110), state.NextDamageAt);
        Assert.Equal((300.0, 400.0), (state.X, state.Y));
        Assert.Equal(StartRadius, state.StartRadius, 6);
        Assert.Null(state.NextAt);
    }

    [Fact]
    public void ZoneStep_TheSafeSpotIsDrawnAnywhereInTheRoom()
    {
        var low = Step(Rules(random: 0), Waiting(), 100).State;
        var high = Step(Rules(random: 1), Waiting(), 100).State;

        Assert.Equal((0.0, 0.0), (low.X, low.Y));
        Assert.Equal((600.0, 800.0), (high.X, high.Y));
    }

    [Fact]
    public void PhaseOf_FollowsTheClock()
    {
        var rules = Rules();
        var state = Active();

        Assert.Equal(ZonePhase.Warning, rules.PhaseOf(state, Start.AddSeconds(5)));
        Assert.Equal(ZonePhase.Closing, rules.PhaseOf(state, Start.AddSeconds(10)));
        Assert.Equal(ZonePhase.Closing, rules.PhaseOf(state, Start.AddSeconds(69)));
        Assert.Equal(ZonePhase.Holding, rules.PhaseOf(state, Start.AddSeconds(70)));
        Assert.Equal(ZonePhase.Holding, rules.PhaseOf(state, Start.AddSeconds(99)));
    }

    [Fact]
    public void PhaseEnd_IsWhenTheCurrentPhaseGivesWayToTheNext()
    {
        var rules = Rules();
        var state = Active();

        Assert.Equal(Start.AddSeconds(10), rules.PhaseEnd(state, Start.AddSeconds(5)));
        Assert.Equal(Start.AddSeconds(70), rules.PhaseEnd(state, Start.AddSeconds(30)));
        Assert.Equal(Start.AddSeconds(100), rules.PhaseEnd(state, Start.AddSeconds(80)));
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(9.9, 500)]
    [InlineData(10, 500)]
    [InlineData(40, 300)]
    [InlineData(70, 100)]
    [InlineData(95, 100)]
    public void RadiusAt_StaysWideInTheWarning_ClosesEvenlyAndThenHolds(double seconds, double radius)
    {
        Assert.Equal(radius, Rules().RadiusAt(Active(), Start.AddSeconds(seconds)), 6);
    }

    [Fact]
    public void ZoneStep_NoDamageInTheWarning()
    {
        var step = Step(Rules(), Active() with { NextDamageAt = Start.AddSeconds(10) }, 9.9);

        Assert.Equal(ZoneAction.None, step.Action);
    }

    [Fact]
    public void ZoneStep_WhileClosing_DamagesOnTheIntervalAtTheCurrentRadius()
    {
        var first = Step(Rules(), Active(), 40);

        Assert.Equal(ZoneAction.Damage, first.Action);
        Assert.Equal(300, first.Radius, 6);
        Assert.Equal(Start.AddSeconds(41), first.State.NextDamageAt);

        var soon = Step(Rules(), first.State, 40.5);
        Assert.Equal(ZoneAction.None, soon.Action);
        var next = Step(Rules(), first.State, 41);
        Assert.Equal(ZoneAction.Damage, next.Action);
    }

    [Fact]
    public void ZoneStep_WhileHolding_GoesOnDamagingAtTheFinalRadius()
    {
        var step = Step(Rules(), Active() with { NextDamageAt = Start.AddSeconds(80) }, 80);

        Assert.Equal(ZoneAction.Damage, step.Action);
        Assert.Equal(100, step.Radius, 6);
    }

    [Fact]
    public void ZoneStep_WhenTheHoldIsOver_EndsAndTheNextIsScheduledFromNow()
    {
        var step = Step(Rules(random: 1), Active(), 100);

        Assert.Equal(ZoneAction.End, step.Action);
        Assert.False(step.State.IsActive);
        Assert.Equal(Start.AddSeconds(300), step.State.NextAt);
    }

    [Fact]
    public void ZoneStep_StaysWhenPlayersLeave_ItIsTheRoomThatForgetsIt()
    {
        var step = Step(Rules(), Active(), 40, players: 0);

        Assert.Equal(ZoneAction.Damage, step.Action);
    }

    [Fact]
    public void ZoneStep_ASmallRoomWithABigFinalRadius_NeverGrowsTheZone()
    {
        var rules = Rules(Zone(zone => zone with { FinalRadius = 900 }));
        var state = Step(rules, Waiting(), 100).State;

        Assert.Equal(900, rules.RadiusAt(state, Start.AddSeconds(100)), 6);
        Assert.Equal(900, rules.RadiusAt(state, Start.AddSeconds(150)), 6);
    }

    [Fact]
    public void ZoneStep_SwitchedOff_ForgetsItAndDoesNothing()
    {
        var off = Rules(Zone(zone => zone with { Enabled = false }));

        var step = Step(off, Active(), 40);

        Assert.Equal(ZoneAction.None, step.Action);
        Assert.Same(ZoneState.None, step.State);
        Assert.Same(ZoneState.None, Step(new WorldEventRules(new WorldEventOptions { Enabled = false, Zone = Zone() }), Active(), 40).State);
    }

    [Fact]
    public void IsOutsideZone_TheEdgeIsInside()
    {
        var centre = new Point(300, 400);

        Assert.False(WorldEventRules.IsOutsideZone(new Point(400, 400), centre, 100, Width, Height));
        Assert.True(WorldEventRules.IsOutsideZone(new Point(401, 400), centre, 100, Width, Height));
        Assert.False(WorldEventRules.IsOutsideZone(new Point(300, 300), centre, 100, Width, Height));
    }

    [Fact]
    public void IsOutsideZone_CountsTheShortWayRoundTheWorldThatWraps()
    {
        // 580 and 20 are 40 apart across the edge of a room 600 wide.
        Assert.False(WorldEventRules.IsOutsideZone(new Point(580, 400), new Point(20, 400), 50, Width, Height));
        Assert.True(WorldEventRules.IsOutsideZone(new Point(580, 400), new Point(20, 400), 30, Width, Height));
        Assert.False(WorldEventRules.IsOutsideZone(new Point(300, 790), new Point(300, 10), 30, Width, Height));
    }

    [Theory]
    [InlineData(0, 10, 10, 60, 30, 100, 1000, 4, 2)]
    [InlineData(10, 5, 10, 60, 30, 100, 1000, 4, 2)]
    [InlineData(5, 10, 0, 60, 30, 100, 1000, 4, 2)]
    [InlineData(5, 10, 10, 1, 30, 100, 1000, 4, 2)]
    [InlineData(5, 10, 10, 60, -1, 100, 1000, 4, 2)]
    [InlineData(5, 10, 10, 60, 30, 5, 1000, 4, 2)]
    [InlineData(5, 10, 10, 60, 30, 100, 50, 4, 2)]
    [InlineData(5, 10, 10, 60, 30, 100, 1000, 0, 2)]
    [InlineData(5, 10, 10, 60, 30, 100, 1000, 4, 0)]
    public void Constructor_ZoneOutOfRange_IsRefused(int min, int max, int warning, int shrink, int hold, int finalRadius, int damageInterval, int damage, int minPlayers)
    {
        var zone = Zone(options => options with
        {
            MinIntervalSeconds = min, MaxIntervalSeconds = max, WarningSeconds = warning, ShrinkSeconds = shrink, HoldSeconds = hold,
            FinalRadius = finalRadius, DamageIntervalMilliseconds = damageInterval, DamageMass = damage, MinPlayers = minPlayers
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventRules(new WorldEventOptions { Zone = zone }));
    }

    [Fact]
    public void Constructor_ABlankRoom_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new WorldEventRules(new WorldEventOptions { Zone = Zone(zone => zone with { Rooms = [" "] }) }));
    }
}
