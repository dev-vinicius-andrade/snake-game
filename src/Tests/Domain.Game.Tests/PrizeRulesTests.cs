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
        var step = _rules.Step(_rules.Definitions[0], PrizeState.None, Start, prizePresent: false, players: 3);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(60), step.State.NextSpawnAt);
    }

    [Fact]
    public void Step_BeforeItIsDue_DoesNothing()
    {
        var state = new PrizeState(Start.AddSeconds(60), null);

        Assert.Equal(PrizeAction.None, _rules.Step(_rules.Definitions[0], state, Start.AddSeconds(59), false, 3).Action);
    }

    [Fact]
    public void Step_WhenDue_SpawnsAndStartsTheLifetime()
    {
        var state = new PrizeState(Start.AddSeconds(60), null);

        var step = _rules.Step(_rules.Definitions[0], state, Start.AddSeconds(60), false, 3);

        Assert.Equal(PrizeAction.Spawn, step.Action);
        Assert.Null(step.State.NextSpawnAt);
        Assert.Equal(Start.AddSeconds(90), step.State.ExpiresAt);
    }

    [Fact]
    public void Step_TooFewPlayers_WaitsWithoutSpawning()
    {
        var state = new PrizeState(Start.AddSeconds(60), null);

        var step = _rules.Step(_rules.Definitions[0], state, Start.AddSeconds(100), false, 1);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void Step_PrizeStillOnTheMap_DoesNothing()
    {
        var state = new PrizeState(null, Start.AddSeconds(90));

        var step = _rules.Step(_rules.Definitions[0], state, Start.AddSeconds(80), true, 3);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void Step_PrizeNotTakenInTime_ExpiresAndSchedulesTheNext()
    {
        var state = new PrizeState(null, Start.AddSeconds(90));

        var step = _rules.Step(_rules.Definitions[0], state, Start.AddSeconds(90), true, 3);

        Assert.Equal(PrizeAction.Expire, step.Action);
        Assert.Equal(Start.AddSeconds(150), step.State.NextSpawnAt);
        Assert.Null(step.State.ExpiresAt);
    }

    [Fact]
    public void Step_PrizeWasEaten_SchedulesTheNext()
    {
        var state = new PrizeState(null, Start.AddSeconds(90));

        var step = _rules.Step(_rules.Definitions[0], state, Start.AddSeconds(70), prizePresent: false, players: 3);

        Assert.Equal(PrizeAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(130), step.State.NextSpawnAt);
        Assert.Null(step.State.ExpiresAt);
    }

    [Fact]
    public void Step_Disabled_NeverSpawns()
    {
        var rules = new PrizeRules(new PrizeOptions { Enabled = false });

        var step = rules.Step(rules.Definitions[0], new PrizeState(Start, null), Start.AddHours(1), false, 5);

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

    [Fact]
    public void Reconfigure_ChangesTheIntervalFromNowOn()
    {
        var rules = new PrizeRules(new PrizeOptions { SpawnIntervalSeconds = 60 });

        rules.Reconfigure(new PrizeOptions { SpawnIntervalSeconds = 10 });

        Assert.Equal(Start.AddSeconds(10), rules.Step(rules.Definitions[0], PrizeState.None, Start, false, 3).State.NextSpawnAt);
    }

    [Fact]
    public void Reconfigure_WithBadOptions_ThrowsAndKeepsTheOldOnes()
    {
        var rules = new PrizeRules(new PrizeOptions { SpawnIntervalSeconds = 60 });

        Assert.Throws<ArgumentOutOfRangeException>(() => rules.Reconfigure(new PrizeOptions { SpawnIntervalSeconds = 0 }));

        Assert.Equal(60, rules.Options.SpawnIntervalSeconds);
    }

    [Fact]
    public void Definitions_WithoutAListAreTheOldSinglePrize()
    {
        var rules = new PrizeRules(new PrizeOptions { SpawnIntervalSeconds = 30, Value = 77 });

        var definition = Assert.Single(rules.Definitions);
        Assert.Equal("prize", definition.Id);
        Assert.Equal(30, definition.SpawnIntervalSeconds);
        Assert.Equal(77, definition.Value);
    }

    [Fact]
    public void Definitions_AListReplacesTheOldFields()
    {
        var rules = new PrizeRules(new PrizeOptions
        {
            Value = 1,
            Prizes = [new() { Id = "a", Name = "A", Value = 5 }, new() { Id = "b", Name = "B", Value = 6 }]
        });

        Assert.Equal(new[] { "a", "b" }, rules.Definitions.Select(definition => definition.Id));
    }

    [Fact]
    public void Step_EachKindHasItsOwnTiming()
    {
        var rules = new PrizeRules(new PrizeOptions
        {
            Prizes = [new() { Id = "fast", Name = "F", SpawnIntervalSeconds = 10, LifetimeSeconds = 5 }, new() { Id = "slow", Name = "S", SpawnIntervalSeconds = 100, LifetimeSeconds = 50 }]
        });

        var fast = rules.Step(rules.Definitions[0], PrizeState.None, Start, false, 3);
        var slow = rules.Step(rules.Definitions[1], PrizeState.None, Start, false, 3);

        Assert.Equal(Start.AddSeconds(10), fast.State.NextSpawnAt);
        Assert.Equal(Start.AddSeconds(100), slow.State.NextSpawnAt);
    }

    [Fact]
    public void Step_AKindThatIsSwitchedOffNeverSpawns()
    {
        var rules = new PrizeRules(new PrizeOptions { Prizes = [new() { Id = "a", Name = "A", Enabled = false }] });

        var step = rules.Step(rules.Definitions[0], new PrizeState(Start, null), Start.AddHours(1), false, 5);

        Assert.Equal(PrizeAction.None, step.Action);
    }

    [Fact]
    public void Constructor_RepeatedKindIdsAreRefused()
    {
        Assert.Throws<ArgumentException>(() => new PrizeRules(new PrizeOptions { Prizes = [new() { Id = "a", Name = "A" }, new() { Id = "a", Name = "B" }] }));
    }

    [Fact]
    public void Constructor_AKindNeedsAnId()
    {
        Assert.Throws<ArgumentException>(() => new PrizeRules(new PrizeOptions { Prizes = [new() { Id = " ", Name = "A" }] }));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    public void Constructor_AColourMustBeSixHexDigits(string color)
    {
        Assert.Throws<ArgumentException>(() => new PrizeRules(new PrizeOptions { Prizes = [new() { Id = "a", Name = "A", Color = color }] }));
    }

    [Fact]
    public void Constructor_EveryKindIsChecked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PrizeRules(new PrizeOptions { Prizes = [new() { Id = "a", Name = "A" }, new() { Id = "b", Name = "B", SpawnIntervalSeconds = 0 }] }));
    }

    [Fact]
    public void Constructor_ABlankRoomIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new PrizeRules(new PrizeOptions { Prizes = [new() { Id = "a", Name = "A", Rooms = [" "] }] }));
    }

    [Fact]
    public void Constructor_NoMoreThanTenKinds()
    {
        var many = Enumerable.Range(0, 11).Select(index => new PrizeDefinition { Id = $"p{index}", Name = "P" }).ToList();

        Assert.Throws<ArgumentException>(() => new PrizeRules(new PrizeOptions { Prizes = many }));
    }
}
