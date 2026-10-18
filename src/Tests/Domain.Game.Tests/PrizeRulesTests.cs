using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class PrizeRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PrizeRules _rules = new(new PrizeOptions
    {
        SpawnIntervalSeconds = 60,
        LifetimeSeconds = 30,
        MinPlayers = 2
    });

    [Fact]
    public void Step_FirstLook_SchedulesTheFirstPrizeAfterTheInterval()
    {
        var step = _rules.Step(PrizeState.None, Start, prizePresent: false, players: 3);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(60), step.State.NextSpawnAt);
    }

    [Fact]
    public void Step_BeforeItIsDue_DoesNothing()
    {
        var state = new PrizeState(Start.AddSeconds(60), null);

        Assert.Equal(PrizeAction.None, _rules.Step(state, Start.AddSeconds(59), false, 3).Action);
    }

    [Fact]
    public void Step_WhenDue_SpawnsAndStartsTheLifetime()
    {
        var state = new PrizeState(Start.AddSeconds(60), null);

        var step = _rules.Step(state, Start.AddSeconds(60), false, 3);

        Assert.Equal(PrizeAction.Spawn, step.Action);
        Assert.Null(step.State.NextSpawnAt);
        Assert.Equal(Start.AddSeconds(90), step.State.ExpiresAt);
    }

    [Fact]
    public void Step_TooFewPlayers_WaitsWithoutSpawning()
    {
        var state = new PrizeState(Start.AddSeconds(60), null);

        var step = _rules.Step(state, Start.AddSeconds(100), false, 1);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void Step_PrizeStillOnTheMap_DoesNothing()
    {
        var state = new PrizeState(null, Start.AddSeconds(90));

        var step = _rules.Step(state, Start.AddSeconds(80), true, 3);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void Step_PrizeNotTakenInTime_ExpiresAndSchedulesTheNext()
    {
        var state = new PrizeState(null, Start.AddSeconds(90));

        var step = _rules.Step(state, Start.AddSeconds(90), true, 3);

        Assert.Equal(PrizeAction.Expire, step.Action);
        Assert.Equal(Start.AddSeconds(150), step.State.NextSpawnAt);
        Assert.Null(step.State.ExpiresAt);
    }

    [Fact]
    public void Step_PrizeWasEaten_SchedulesTheNext()
    {
        var state = new PrizeState(null, Start.AddSeconds(90));

        var step = _rules.Step(state, Start.AddSeconds(70), prizePresent: false, players: 3);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(130), step.State.NextSpawnAt);
        Assert.Null(step.State.ExpiresAt);
    }

    [Fact]
    public void Step_Disabled_NeverSpawns()
    {
        var rules = new PrizeRules(new PrizeOptions { Enabled = false });

        var step = rules.Step(new PrizeState(Start, null), Start.AddHours(1), false, 5);

        Assert.Equal(PrizeAction.None, step.Action);
    }

    [Theory]
    [InlineData(0, 30, 20, 5, 1)]
    [InlineData(60, 0, 20, 5, 1)]
    [InlineData(60, 30, 0, 5, 1)]
    [InlineData(60, 30, 20, -1, 1)]
    [InlineData(60, 30, 20, 5, 0)]
    public void Constructor_RejectsValuesThatCannotWork(int interval, int lifetime, long value, int credit, int minPlayers)
    {
        var options = new PrizeOptions
        {
            SpawnIntervalSeconds = interval,
            LifetimeSeconds = lifetime,
            Value = value,
            FoodsCredit = credit,
            MinPlayers = minPlayers
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new PrizeRules(options));
    }
}
